namespace PasifeLua.Bytecode
{
    public enum LuaOps
    {
        MOVE,
        LOADK,
        LOADKX,
        LOADBOOL,
        LOADNIL,
        GETUPVAL,
        
        GETTABUP,
        GETTABLE,
        
        SETTABUP,
        SETUPVAL,
        SETTABLE,
        
        NEWTABLE,
        SELF,
        
        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        POW,
        UNM,
        NOT,
        LEN,
        
        CONCAT,
        
        JMP,
        EQ,
        LT,
        LE,
        
        TEST,
        TESTSET,
        
        CALL,
        TAILCALL,
        RETURN,
        
        FORLOOP,
        FORPREP,
        
        TFORCALL,
        TFORLOOP,
        
        SETLIST,
        
        CLOSURE,
        
        VARARG,
        
        EXTRAARG,
    }
}