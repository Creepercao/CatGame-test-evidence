*** Settings ***
Documentation    Actual non-batch Development Player screenshots and a 30-second 100-actor performance observation. Requires a QA build.
Library    Process
Library    OperatingSystem

*** Variables ***
${PROJECT}    <PROJECT>
${SYSTEM_POINTER}    %{CATGAME_QA_SYSTEM_POINTER=0}
${FLOW}    %{CATGAME_QA_FLOW=0}

*** Test Cases ***
Player Captures All Sizes And Completes Roster Observation
    ${root}=    Set Variable    ${OUTPUT DIR}/player
    Create Directory    ${root}
    ${run}=    Run Process    ${PROJECT}/Build/QA/CatGameQA.exe    -screen-fullscreen    0    -logFile    ${OUTPUT DIR}/player.log
    ...    cwd=${PROJECT}    env:CATGAME_PLAYER_QA_ROOT=${root}    env:CATGAME_QA_SYSTEM_POINTER=${SYSTEM_POINTER}    env:CATGAME_QA_FLOW=${FLOW}    timeout=240s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
    ${intro}=    Get File    ${root}/intro.txt
    Should Contain    ${intro}    introCompleted=true
    Should Contain    ${intro}    noClickThrough=true
    Should Contain    ${intro}    pausedProduction=true
    File Should Exist    ${root}/toast-visible-1280x720.png
    File Should Exist    ${root}/toast-fading-1280x720.png
    FOR    ${size}    IN    1920x1080    1280x720    1024x768
        FOR    ${page}    IN    home    shop-items    shop-bottom    achievements-top    achievements-bottom    catalog-top    catalog-bottom
            File Should Exist    ${root}/${page}-${size}.png
        END
        File Should Exist    ${root}/intro-welcome-${size}.png
    END
    File Should Exist    ${root}/roster-440-1280x720.png
    ${items}=    Get File    ${root}/items.txt
    Should Contain    ${items}    purchases=100
    Should Contain    ${items}    visible=20
    Should Contain    ${items}    capRejected=20
    FOR    ${size}    IN    1920x1080    1280x720    1024x768
        File Should Exist    ${root}/all-items-level5-${size}.png
    END
    ${metrics}=    Get File    ${root}/performance.txt
    Log    ${metrics}
    Should Contain    ${metrics}    visible=100
    Should Contain    ${metrics}    owned=440
    Should Contain    ${metrics}    boundsErrors=0
    Should Contain    ${metrics}    roomFootErrors=0
    Should Contain    ${metrics}    furnitureDepthErrors=0
    Should Contain    ${metrics}    roomActors=100
    IF    '${SYSTEM_POINTER}' == '1'
        Should Contain    ${metrics}    nativePointerHits=3
    ELSE
        Log    System pointer interaction was not requested; this run proves screenshots and geometry only.    WARN
    END
    Should Contain    ${metrics}    ${root}
    File Should Exist    ${root}/CatGame/save.json
    IF    '${FLOW}' == '1'
        Should Contain    ${intro}    completedRuns=2
        FOR    ${size}    IN    1920x1080    1280x720    1024x768
            File Should Exist    ${root}/menu-fresh-${size}.png
        END
        ${flow}=    Get File    ${root}/flow.txt
        Log    ${flow}
        Should Contain    ${flow}    flowPassed=true
        Should Contain    ${flow}    earlyTwoClicksBlocked=true
        Should Contain    ${flow}    clickPacingMatched=true
    END
