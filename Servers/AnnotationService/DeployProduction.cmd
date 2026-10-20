@rem Copies the AnnotationService (WCF) binaries to the IIS deployment folder.
@rem Usage: DeployProduction.cmd [SourceBin] [TargetBin]
@rem   SourceBin  default C:\src\git\Viking\Servers\AnnotationService\AnnotationService\bin
@rem   TargetBin  default \\OpR-Marc-WebSv2\C$\Services\Release\AnnotationService\bin
@rem web.config and webText.config on the target are never overwritten.
@rem deploy\Deploy-VikingIis.ps1 -Action Annotation calls this with paths from the viking-iis run env.
@rem Exit code is robocopy's: 0-7 success, 8 or more failure.
@setlocal
@set "SRC=%~1"
@if "%SRC%"=="" set "SRC=C:\src\git\Viking\Servers\AnnotationService\AnnotationService\bin"
@set "DST=%~2"
@if "%DST%"=="" set "DST=\\OpR-Marc-WebSv2\C$\Services\Release\AnnotationService\bin"
robocopy "%SRC%" "%DST%" /mt:8 /XF web.config /XF webText.config
@endlocal & exit /b %ERRORLEVEL%
