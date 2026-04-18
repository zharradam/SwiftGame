@echo off
echo Building...
cmd /c ng build --configuration production --base-href "/"
echo Build step complete.
if exist "dist\SwiftGame.Client\browser\index.html" (
  echo Deploying to GitHub Pages...
  cmd /c npx angular-cli-ghpages --dir=dist/SwiftGame.Client/browser
  echo Done.
) else (
  echo Build failed - index.html not found. Aborting.
)
pause