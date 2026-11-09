@echo off
chcp 65001 >nul
echo ============================================
echo  Trae CN 聊天记录一键清理
echo  注意：本脚本会强制关闭 Trae CN！
echo  请先保存好编辑器里的文件，再按任意键继续。
echo ============================================
pause

echo.
echo [1/5] 正在关闭 Trae CN ...
taskkill /F /IM "Trae CN.exe" >nul 2>&1
timeout /t 3 /nobreak >nul

set "TCD=%APPDATA%\Trae CN"
set "TCU=%USERPROFILE%\.trae-cn"

echo [2/5] 删除聊天数据库 ...
del /f /q "%TCD%\ModularData\ai-agent\database.db" 2>nul
del /f /q "%TCD%\ModularData\ai-agent\database.db-shm" 2>nul
del /f /q "%TCD%\ModularData\ai-agent\database.db-wal" 2>nul

echo [3/5] 清理各工作区会话缓存键 ...
python "%~dp0clear_trae_chat.py"

echo [4/5] 删除会话工作目录和事件缓存 ...
for /d %%D in ("%TCU%\work\*") do rd /s /q "%%D" 2>nul
for /d %%D in ("%TCU%\hub_event_cache\TraeCode\*") do rd /s /q "%%D" 2>nul
for /d %%D in ("%TCU%\hub_event_cache\TraeWork\*") do rd /s /q "%%D" 2>nul

echo [5/5] 删除聊天中上传的附件 ...
for /d %%D in ("%TCU%\attachments\*") do rd /s /q "%%D" 2>nul

echo.
echo 完成！本地聊天记录已全部清除，重新打开 Trae CN 即可。
echo （若历史记录从云端同步恢复，只能在新界面里逐条删除。）
pause
