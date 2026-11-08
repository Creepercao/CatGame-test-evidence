*** Settings ***
Library    Process
Library    OperatingSystem

*** Test Cases ***
Pixel Momo Dialogue Fits Three Actual Player Sizes
    ${run}=    Run Process    <PROJECT>/Build/QA/CatGameQA.exe    -screen-fullscreen    0    -logFile    ${OUTPUT DIR}/player.log
    ...    env:CATGAME_PLAYER_QA_ROOT=${OUTPUT DIR}/player    env:CATGAME_QA_INTRO_ONLY=1    timeout=90s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
    FOR    ${size}    IN    1920x1080    1280x720    1024x768
        File Should Exist    ${OUTPUT DIR}/player/intro-welcome-${size}.png
    END
    ${intro}=    Get File    ${OUTPUT DIR}/player/intro.txt
    Should Contain    ${intro}    introCompleted=true
    Should Contain    ${intro}    noClickThrough=true
    Should Contain    ${intro}    pausedProduction=true
