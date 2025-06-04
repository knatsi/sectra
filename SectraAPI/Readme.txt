Inställning av folder i appsettings.json
Input via POST body json enligt spec Sectra
Vid input letar den först i folder efter fil med patientid och sen matchande UID. 
    Om den inte hittar någon fil med matchning på båda letar den igenom alla filer efter matckande UID (tar dock längre tid)
Vid funnen rätt XML-fil sorterar den ut parameterdatan. Den lägger också till "_ResultNo" efter beskrivningen. 
    I ukg-maskinen kommer alltid det som är det som är förvalt som huvudvärde kommer alltid ha ResultNo -1. 
    ParameterID - "Värde" 
