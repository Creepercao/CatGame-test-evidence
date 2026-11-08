*** Settings ***
Library    ../libraries/UnityTestRunner.py    ${UNITY_EXE}    ${PROJECT_PATH}    ${OUTPUT DIR}/unity-roaming
Test Tags    regression    roaming

*** Variables ***
${UNITY_EXE}    %{UNITY_EXE=D:\Unity\2022.3.21f1\Editor\Unity.exe}
${PROJECT_PATH}    %{CATGAME_PROJECT=<PROJECT>}

*** Test Cases ***
Surface Assignment Keeps Initial Roaming Target
    ${result}=    Run Tests    PlayMode    Assigning_open_walk_surface_preserves_initial_roaming_distance
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[passed]    1
