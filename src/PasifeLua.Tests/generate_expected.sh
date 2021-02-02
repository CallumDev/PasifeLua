 #!/bin/bash
 for filename in ./Tests/*.lua; do
    output=./Tests/expected/"${filename##*/}".txt
    echo "$filename -> $output"
    lua5.2 $filename > $output
done
