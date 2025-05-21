@echo off
echo Running Excel to JSON converter...

REM Assuming Python is in your PATH
REM If not, you might need to provide the full path to python.exe
REM e.g., C:\Python39\python.exe excel_to_json_converter.py

python excel_to_json_converter.py

echo.
echo Conversion process finished.
echo Check the console for messages and Resources/lines/endings.json for the output.
echo.
pause 