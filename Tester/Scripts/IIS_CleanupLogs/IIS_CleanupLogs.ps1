#ls -r | sort LastWriteTime -desc | select -skip 4 | rm
#ls -r | sort LastWriteTime -desc | select -skip 10
$dirs = Get-ChildItem W3SVC* -Dir
ForEach ($dir in $dirs) {
    $dir |
    Get-ChildItem -File -include "u_ex*.log" |
    #Sort LastWriteTime -Desc |
    Sort Name -Desc |
    Select-Object -Skip 365 |
    #Select FullName
    rm
}

