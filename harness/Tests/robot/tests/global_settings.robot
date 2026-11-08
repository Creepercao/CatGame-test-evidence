*** Settings ***
Library    Process
Library    OperatingSystem

*** Test Cases ***
Actual Settings Windows Four Resolutions And Process Restart
    ${run}=    Run Process    <PROJECT>/Build/QA/CatGameQA.exe    -screen-fullscreen    0    -logFile    ${OUTPUT DIR}/settings-player.log
    ...    env:CATGAME_PLAYER_QA_ROOT=${OUTPUT DIR}/player    env:CATGAME_QA_SETTINGS_ONLY=1    timeout=90s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
    File Should Exist    ${OUTPUT DIR}/player/settings.txt
    ${restart}=    Run Process    <PROJECT>/Build/QA/CatGameQA.exe    -screen-fullscreen    0    -logFile    ${OUTPUT DIR}/restart-player.log
    ...    env:CATGAME_PLAYER_QA_ROOT=${OUTPUT DIR}/player    env:CATGAME_QA_SETTINGS_ONLY=1    env:CATGAME_QA_SETTINGS_RESTART=1    timeout=60s    on_timeout=terminate
    Should Be Equal As Integers    ${restart.rc}    0
    File Should Exist    ${OUTPUT DIR}/player/restart.txt
    FOR    ${size}    IN    1024x768    1280x720    1600x900    1920x1080
        File Should Exist    ${OUTPUT DIR}/player/menu-settings-${size}.png
        File Should Exist    ${OUTPUT DIR}/player/game-settings-${size}.png
    END
