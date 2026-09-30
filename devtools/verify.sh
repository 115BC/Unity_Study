#!/usr/bin/env bash
# 离线编译校验:编辑器开着(它占着项目锁)时用来确认 C# 至少能编过。
# ⚠ Unity 每次重启都会清空 Temp/,这个脚本已经丢过好几次了 —— 内容全文留在这儿,照抄重建即可。
#   现在有了更省事的办法:`git show HEAD:Temp/verify.sh` 拿不回来(Temp 不入库),但照抄下面这段就行。
#   三个坑:1) csc 只认 -r:<路径>(冒号);2) 路径要 Windows 写法(csc 把 / 当选项前缀);
#          3) csc 中文输出是 GBK,要 iconv。4) 门面必须是 Data/Managed/UnityEngine/UnityEngine.dll。
set -u
UM="/f/applications/unity/unity2022/2022.3.62f3c1/Editor"
PM="/d/code/unity/60slike"
OUTM="$PM/Temp/verify_out"
mkdir -p "$OUTM"
winp() { sed -E 's#^/([a-zA-Z])/#\U\1:/#' <<< "$1"; }

REFS=()
for d in "$UM"/Data/Managed/UnityEngine/UnityEngine.*.dll; do REFS+=(-r:"$(winp "$d")"); done
REFS+=(-r:"$(winp "$UM/Data/Managed/UnityEngine/UnityEngine.dll")")
REFS+=(-r:"$(winp "$UM/Data/Managed/UnityEditor.dll")")
REFS+=(-r:"$(winp "$UM/Data/NetStandard/ref/2.1.0/netstandard.dll")")
REFS+=(-r:"$(winp "$PM/Library/ScriptAssemblies/UnityEngine.UI.dll")")
REFS+=(-r:"$(winp "$PM/Library/ScriptAssemblies/Unity.Timeline.dll")")

find "$PM/Assets/Scripts" -name '*.cs' | sed -E 's#^/([a-zA-Z])/#\U\1:/#' > "$OUTM/srcs.txt"

dotnet "$(winp "$UM/Data/DotNetSdkRoslyn/csc.dll")" \
  -nologo -target:library -nostdlib -noconfig \
  -define:UNITY_2022_3_OR_NEWER,UNITY_2022_3,UNITY_64,UNITY_STANDALONE_WIN,UNITY_EDITOR,DEBUG,TRACE \
  -nowarn:0169,0414,0649,0618,0219 \
  "${REFS[@]}" -out:"$(winp "$OUTM/verify.dll")" "@$(winp "$OUTM/srcs.txt")" > "$OUTM/log.txt" 2>&1

iconv -f GBK -t UTF-8 "$OUTM/log.txt" > "$OUTM/log8.txt" 2>/dev/null || cp "$OUTM/log.txt" "$OUTM/log8.txt"
grep -E "error CS" "$OUTM/log8.txt" | head -30
echo "--- sources: $(wc -l < "$OUTM/srcs.txt") | error lines: $(grep -c 'error CS' "$OUTM/log8.txt")"
