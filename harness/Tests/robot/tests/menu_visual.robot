*** Settings ***
Library    Process
Library    OperatingSystem

*** Test Cases ***
Story Menu Fits All Three Window Sizes
    ${run}=    Run Process    <PROJECT>/Build/QA/CatGameQA.exe    -screen-fullscreen    0    -logFile    ${OUTPUT DIR}/player.log
    ...    env:CATGAME_PLAYER_QA_ROOT=${OUTPUT DIR}/player    env:CATGAME_QA_MENU_ONLY=1    timeout=90s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
    FOR    ${size}    IN    1920x1080    1280x720    1024x768
        File Should Exist    ${OUTPUT DIR}/player/menu-fresh-${size}.png
    END
