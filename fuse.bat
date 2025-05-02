@echo off
REM Runs the Fuse tool for the Cosmocrush project

echo Starting Fuse process for Cosmocrush...
echo Outputting to: D:\Parsa Stuff\Visual Studio\Dottle\MergedCodeForAI.txt
echo Scanning folders:
echo   - D:\Parsa Stuff\Visual Studio\Dottle\Dottle
echo ==================================================

Fuse.exe "D:\Parsa Stuff\Visual Studio\Dottle\MergedCodeForAI.txt" "D:\Parsa Stuff\Visual Studio\Dottle\Dottle"

echo ==================================================
echo Fuse process finished. Press any key to close this window.
pause