@echo off
echo Building...
cmd /c ng build --configuration production --base-href "/"
echo Build step complete.
if exist "dist\SwiftGame.Client\browser\index.html" (
  echo Adding CNAME...
  echo swiftology.uk > dist\SwiftGame.Client\browser\CNAME
  echo Deploying to GitHub Pages...
  cmd /c ngh --dir=dist/SwiftGame.Client/browser
  echo Done.
) else (
  echo Build failed - index.html not found. Aborting.
)
pause