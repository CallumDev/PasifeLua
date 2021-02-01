//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

//HEAVILY MODIFIED FROM NEOLUA (https://github.com/neolithos/neolua/blob/master/NeoLua/LuaTable.cs)
using System;
using System.Collections.Generic;

namespace PasifeLua
{
	public class LuaTable
	{
		private static long _sid = 0x55FA75EC; //looks realistic

		private ulong ID;
		public LuaTable()
		{
			ID = unchecked ((ulong) System.Threading.Interlocked.Increment(ref _sid));
		}
		private struct LuaTableEntry
		{
			public int hashCode;
			public LuaValue key;
			public LuaValue value;
			public int nextHash;
		}
		
		
		private LuaTable metaTable = null;                        // Currently attached metatable

		private LuaTableEntry[] entries = emptyLuaEntries;        // Key/Value part of the lua-table
		private int[] hashLists = emptyIntArray;                  // Hashcode entry point
		private LuaValue[] arrayList = emptyValueArray;            // List with the array elements (this array is ZERO-based)

		private int freeTopIndex = -1;                            // Start of the free lists

		private int arrayLength = 0;                              // Current length of the array list

		private int count = 0;                                    // Number of element in the Key/Value part

		private int version = 0;                                  // version for the data
		
		private static int NextArraySize(int currentLength, int capacity)
		{
			if (currentLength == Int32.MaxValue)
				throw new OverflowException();
			if (currentLength == 0)
				currentLength = 16;

			Resize:
			currentLength = unchecked(currentLength << 1);

			if (currentLength == Int32.MinValue)
				currentLength = Int32.MaxValue;
			else if (capacity > currentLength)
				goto Resize;

			return currentLength;
		} // func NextArraySize
		
		private int InsertValue(LuaValue key, int hashCode, LuaValue value)
		{

			if (freeTopIndex == -1) // entry list is full -> enlarge
				ResizeEntryList();

			// get free item
			var freeItemIndex = freeTopIndex;
			freeTopIndex = entries[freeTopIndex].nextHash;

			// set the values
			entries[freeItemIndex].key = key;
			entries[freeItemIndex].value = value;

			// create the hash list
			var hashIndex = (entries[freeItemIndex].hashCode = hashCode) % hashLists.Length;
			entries[freeItemIndex].nextHash = hashLists[hashIndex];
			hashLists[hashIndex] = freeItemIndex;

			count++;
			version++;

			return freeItemIndex;
		} 
		
		private int FindKey(LuaValue key, int hashCode)
		{
			var hashLength = hashLists.Length;
			if (hashLength == 0)
				return -1;

			var hashIndex = hashCode % hashLength;
			for (var i = hashLists[hashIndex]; i >= 0; i = entries[i].nextHash)
			{
				if (entries[i].hashCode == hashCode && entries[i].key == key) 
					return i;
			}
			return ~hashIndex;
		} 

		private void RemoveValue(int index)
		{

			var hashCode = entries[index].hashCode;
			var hashIndex = hashCode % hashLists.Length;

			// remove the item from hash list
			var currentIndex = hashLists[hashIndex];
			if (currentIndex == index)
			{
				hashLists[hashIndex] = entries[index].nextHash;
			}
			else
			{
				while (true)
				{
					var nextIndex = entries[currentIndex].nextHash;
					if (nextIndex == index)
					{
						entries[currentIndex].nextHash = entries[index].nextHash; // remove item from lest
						break;
					}
					currentIndex = nextIndex;

					if (currentIndex == -1)
						throw new InvalidOperationException();
				}
			}

			// add to free list
			entries[index].hashCode = -1;
			entries[index].key = new LuaValue();
			entries[index].value = new LuaValue();
			entries[index].nextHash = freeTopIndex;
			freeTopIndex = index;

			count--;
			version++;
		} // proc RemoveValue

		private void ResizeEntryList(int capacity = 0)
		{
			var newEntries = new LuaTableEntry[NextArraySize(entries.Length, capacity)];

			// copy the old values
			Array.Copy(entries, 0, newEntries, 0, entries.Length);

			// create the free list for the new entries
			freeTopIndex = entries.Length;
			var length = newEntries.Length - 1;
			for (var i = freeTopIndex; i < length; i++)
			{
				newEntries[i].hashCode = -1;
				newEntries[i].nextHash = i + 1;
			}
			// set the last element
			newEntries[length].hashCode = -1;
			newEntries[length].nextHash = -1;

			// real length
			length++;

			// update the array
			entries = newEntries;

			// create the hash table new
			hashLists = new int[length];
			for (var i = 0; i < hashLists.Length; i++)
				hashLists[i] = -1;

			// rehash all entries
			for (var i = 0; i < freeTopIndex; i++)
			{
				int iIndex = entries[i].hashCode % hashLists.Length;
				entries[i].nextHash = hashLists[iIndex];
				hashLists[iIndex] = i;
			}
		}
		private int FindKey(int index)
			=> FindKey(new LuaValue(LuaType.Number, index), index.GetHashCode() & 0x7FFFFFFF);

		private void SetIndexCopyValuesToArray(LuaValue[] newArray, int startAt)
		{
			if (newArray.Length - startAt < entries.Length) // choose the less expensive way to copy the values, try to find values
			{
				for (var i = startAt; i < newArray.Length; i++)
				{
					var entryIndex = FindKey(i + 1);
					if (entryIndex >= 0)
					{
						newArray[i] = entries[entryIndex].value;
						RemoveValue(entryIndex);
						count++;
					}
				}
			}
			else // go through the array
			{
				for (var i = 0; i < entries.Length; i++)
				{
					if (IsIntKey(entries[i].key, out var k) && startAt < k && k <= newArray.Length)
					{
						newArray[k - 1] = entries[i].value;
						RemoveValue(i);
						count++;
					}
				}
			}
		} // func SetIndexCopyValuesToArray

		static bool IsIntKey(LuaValue value, out int k)
		{
			k = 0;
			if (value.Type != LuaType.Number) return false;
			int i = 0;
			if (value.number >= 0 && value.number <= Int32.MaxValue &&
			    (i = (int)value.number) == value.number)
			{
				k = i;
				return true;
			}
			return false;
		}

		static bool IsIntKey(int x, out int k)
		{
			if (x >= 0 && x < Int32.MaxValue)
			{
				k = x;
				return true;
			}
			k = 0;
			return false;
		}

		bool CallTM__newIndex(LuaState state, LuaValue key, LuaValue value)
		{
			LuaValue mt = TM.GetTMByTable(state, this, TMS.NEWINDEX);
			if (mt.IsNil()) return true;
			var lv = new LuaValue(this);
			state.CallTagMethod(ref mt, ref lv, ref key, ref value);
			return false;
		}
		
		void SetArrayValue(int index, LuaValue value, LuaState state)
		{
			var arrayIndex = index - 1;
			if (unchecked((uint)arrayIndex < arrayList.Length)) // with in the current allocated array
			{
				var oldValue = arrayList[arrayIndex];
				if (value.IsNil()) // remove the value
				{
					if (!oldValue.IsNil())
					{
						arrayList[arrayIndex] = new LuaValue();
						if (arrayIndex < arrayLength)
							arrayLength = arrayIndex; // iArrayLength = iIndex - 1
						count--;
						version++;
					}
				}
				else if (state == null // always set a value
					|| !oldValue.IsNil() // reset the value
					|| CallTM__newIndex(state, new LuaValue(index), value)) // no value, notify __newindex to set the array element
				{
					if (oldValue.IsNil())
						count++;
					arrayList[arrayIndex] = value;
					version++;
					// correct the array length
					if (arrayLength == arrayIndex) // iArrayLength == iIndex - 1
					{
						// search for the end of the array
						arrayLength = index;
						while (arrayLength + 1 <= arrayList.Length && !arrayList[arrayLength].IsNil())
							arrayLength++;
						// are the more values behind the array
						if (arrayLength == arrayList.Length)
						{
							var collected = new List<LuaValue>();

							// collect values
							int entryIndex;
							while ((entryIndex = FindKey(arrayLength + 1)) >= 0)
							{
								collected.Add(entries[entryIndex].value);
								RemoveValue(entryIndex);
								count++;

								arrayLength++;
							}

							// append the values to the array
							if (collected.Count > 0)
							{
								// enlarge array part, with the new values
								var newArray = new LuaValue[NextArraySize(arrayList.Length, arrayLength)];
								// copy the old array
								Array.Copy(arrayList, 0, newArray, 0, arrayList.Length);
								// copy the new array content
								collected.CopyTo(newArray, arrayList.Length);
								// collect values for buffer
								SetIndexCopyValuesToArray(newArray, arrayLength);

								arrayList = newArray;
							}
						}
					}
				}
			}
			else if (arrayIndex == arrayLength && !value.IsNil()) // enlarge array part
			{
				if (!value.IsNil() && (state == null || CallTM__newIndex(state, new LuaValue(index), value)))
				{
					// create a new enlarged array
					var newArray = new LuaValue[NextArraySize(arrayList.Length, 0)];
					Array.Copy(arrayList, 0, newArray, 0, arrayList.Length);

					// copy the values from the key/value part to the array part
					SetIndexCopyValuesToArray(newArray, arrayList.Length);

					arrayList = newArray;

					// set the value in the index
					SetArrayValue(index, value, null);
				}
			}
			else // set the value in key/value part
			{
				var hashCode = index.GetHashCode() & 0x7FFFFFFF;
				var entryIndex = FindKey(new LuaValue(LuaType.Number, index), hashCode);
				if (entryIndex >= 0)
				{
					if (value.IsNil())
					{
						RemoveValue(entryIndex);
					}
					else
					{
						entries[entryIndex].value = value;
						version++;
					}
				}
				else if (state == null || CallTM__newIndex(state, new LuaValue(index), value))
					InsertValue(new LuaValue(LuaType.Number, index), hashCode, value);
				
			}

		} // func SetArrayValue

		LuaValue GetArrayValue(int index)
		{
			var arrayIndex = index - 1;
			if (unchecked((uint)arrayIndex < arrayList.Length)) // part of array
			{
				if (arrayIndex < arrayLength)
					return arrayList[arrayIndex];
			}
			else // check the hash part
			{
				var entryIndex = FindKey(index);
				if (entryIndex >= 0) // get the hashed value
					return entries[entryIndex].value;
			}
			return new LuaValue();
		} // func SetArrayValue

		public void SetValue(LuaValue key, LuaValue value, LuaState state = null)
		{
			if (key.IsNil()) throw new ArgumentNullException();
			if (IsIntKey(key, out var index))
			{
				// is a array element
				SetArrayValue(index, value, state);
			}
			else // something else
			{
				var hashCode = key.GetHashCode() & 0x7FFFFFFF;
				index = FindKey(key, hashCode); // find the value

				if (value.IsNil()) // remove value
					RemoveValue(index);
				else if (index < 0 && (state == null || CallTM__newIndex(state, new LuaValue(index), value))) // insert value
					InsertValue(key, hashCode, value);
				else // update value
					entries[index].value = value;
			}
		}

		public LuaValue GetValue(LuaValue key)
		{
			if (key.IsNil()) throw new ArgumentNullException();
			else if (IsIntKey(key, out var index))
				return GetArrayValue(index);
			else
			{
				index = FindKey(key, key.GetHashCode() & 0x7FFFFFFF);
				if (index < 0)
				{
					return new LuaValue();
				}
				else
					return entries[index].value;
			}
		}
		
		public IEnumerator<KeyValuePair<LuaValue, LuaValue>> GetEnumerator()
		{
			int iVersion = this.version;

			// enumerate the array part
			for (int i = 0; i < arrayList.Length; i++)
			{
				if (iVersion != this.version)
					throw new InvalidOperationException();

				if (!arrayList[i].IsNil())
					yield return new KeyValuePair<LuaValue, LuaValue>(new LuaValue(LuaType.Number,i + 1), arrayList[i]);
			}

			// enumerate the hash part
			for (int i = 0; i < entries.Length; i++)
			{
				if (iVersion != this.version)
					throw new InvalidOperationException();

				if (entries[i].hashCode != -1)
					yield return new KeyValuePair<LuaValue, LuaValue>(entries[i].key, entries[i].value);
			}
		} 

		public LuaValue this[int index] { get => GetArrayValue(index); set => SetArrayValue(index, value, null); }

		public LuaValue this[LuaValue key] { get => GetValue(key); set => SetValue(key, value); }
		
		public LuaValue this[string key] { get => GetValue(new LuaValue(key)); set => SetValue(new LuaValue(key), value); }
		public int Length => arrayLength;
		
		/// <summary>Access to the __metatable</summary>
		public LuaTable MetaTable { get => metaTable; set => metaTable = value; }
		

		private static readonly LuaTableEntry[] emptyLuaEntries = new LuaTableEntry[0];
		private static readonly LuaValue[] emptyValueArray = new LuaValue[0];
		private static readonly int[] emptyIntArray = new int[0];
		
		private void ArrayOnlyInsert(int iIndex, LuaValue value)
		{
			if (iIndex < 0 || iIndex > arrayLength)
				throw new ArgumentOutOfRangeException();

			LuaValue last;
			if (iIndex == arrayLength)
				last = value;
			else
			{
				last = arrayList[arrayLength - 1];
				if (iIndex != arrayLength - 1)
					Array.Copy(arrayList, iIndex, arrayList, iIndex + 1, arrayLength - iIndex - 1);
				arrayList[iIndex] = value;
			}

			SetArrayValue(arrayLength + 1, last, null);
		} // proc ArrayOnlyInsert 

		
		public static void insert(LuaTable t, LuaValue value)
		{
			// the pos is optional
			insert(t, new LuaValue(LuaType.Number,t.Length <= 0 ? 1 : t.Length + 1), value);
		} // proc insert

		public static void insert(LuaTable t, LuaValue pos, LuaValue value)
		{
			if (value.IsNil() && pos.IsNil()) // check for wrong overload
				insert(t, pos);
			else
			{
				// insert the value at the position
				int index;
				if (IsIntKey(pos, out index) && index >= 1 && index <= t.arrayLength + 1)
					t.ArrayOnlyInsert(index - 1, value);
				else
					t.SetValue(pos, value, null);
			}
		} 
		
		public static void move(LuaState state, LuaTable t1, int f, int e, int t)
		{
			move(state, t1, f, e, t, t1);
		} // proc move

		public static void move(LuaState state, LuaTable t1, int f, int e, int t, LuaTable t2)
		{
			/*if (f < 0)
				throw new ArgumentOutOfRangeException("f");
			if (t < 0)
				throw new ArgumentOutOfRangeException("t");
			if (f > e)
				return;

			while (f < e)
			{
				t2.SetValue(new LuaValue(t++), t1.GetValue(new LuaValue(f++), state), state);
			}*/
		} // proc move
		
		public static LuaValue remove(LuaTable t)
			=> remove(t, t.Length);
		
		private void ArrayOnlyRemoveAt(int index)
		{
			if (index < 0 || index >= arrayLength)
				throw new ArgumentOutOfRangeException();

			Array.Copy(arrayList, index + 1, arrayList, index, arrayLength - index - 1);
			arrayList[--arrayLength] = new LuaValue();

			version++;
		} // func ArrayOnlyRemoveAt
		
		public static LuaValue remove(LuaTable t, int pos)
		{
			LuaValue r;
			int index;
			if (IsIntKey(pos, out index))
			{
				if (index >= 1 && index <= t.arrayLength)  // remove the element and shift the follower
				{
					r = t.arrayList[index - 1];
					t.ArrayOnlyRemoveAt(index - 1);
				}
				else
				{
					r = t.GetArrayValue(index);
					t.SetArrayValue(index, new LuaValue(), null); // just remove the element
				}
			}
			else
			{
				r = t.GetValue(new LuaValue(LuaType.Number, pos));
				t.SetValue(new LuaValue(LuaType.Number, pos), new LuaValue(), null); // just remove the key
			}
			return r;
		} // proc remove

		public override string ToString()
		{
			return $"table: 0x{ID:X}";
		}
	}
}
