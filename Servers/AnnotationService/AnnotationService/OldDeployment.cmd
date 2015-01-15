copy "$(ProjectDir)bin\Debug\*.dll" "\WebServices\TextDebug\bin"
copy "$(ProjectDir)bin\Debug\*.pdb" "\WebServices\TextDebug\bin"
copy "$(ProjectDir)bin\Debug\*.svc" "\WebServices\TextDebug"
copy "$(ProjectDir)bin\Debug\web.config" "\WebServices\TextDebug\web.config"

copy "$(ProjectDir)bin\Debug\*.dll" "\WebServices\BinaryDebug\bin"
copy "$(ProjectDir)bin\Debug\*.pdb" "\WebServices\BinaryDebug\bin"
copy "$(ProjectDir)bin\Debug\*.svc" "\WebServices\BinaryDebug\"
copy "$(ProjectDir)bin\Debug\webBinary.config" "\WebServices\BinaryDebug\web.config"

copy $(ProjectDir)bin\Release\*.dll \WebServices\Text\bin
copy $(ProjectDir)bin\Release\*.pdb \WebServices\Text\bin
copy $(ProjectDir)bin\Release\*.svc \WebServices\Text\
copy $(ProjectDir)bin\Release\web.config \WebServices\Text\web.config

copy $(ProjectDir)bin\Release\*.dll \WebServices\Binary\bin
copy $(ProjectDir)bin\Release\*.pdb \WebServices\Binary\bin
copy $(ProjectDir)bin\Release\*.svc \WebServices\Binary\
copy $(ProjectDir)bin\Release\webBinary.config \WebServices\Binary\web.config