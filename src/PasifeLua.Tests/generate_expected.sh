 #!/bin/bash
 for filename in ./Tests/*.lua; do
    output=./Tests/expected/"${filename##*/}".txt
    echo "$filename -> $output"
    lua5.2 -e "package.path ='?;?.lua;./Tests/Modules/?.lua;'" $filename > $output
done
