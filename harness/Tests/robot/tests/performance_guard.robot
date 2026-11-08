*** Settings ***
Documentation    Functional stress supplement; Editor pointer dispatch, separate from Player timing samples.
Library    ../libraries/UnityTestRunner.py    ${UNITY_EXE}    ${PROJECT_PATH}    ${OUTPUT DIR}/unity-guard
Test Tags    stress    unity    cg    input

*** Variables ***
${UNITY_EXE}    %{UNITY_EXE=D:\Unity\2022.3.21f1\Editor\Unity.exe}
${PROJECT_PATH}    %{CATGAME_PROJECT=<PROJECT>}

*** Test Cases ***
CG Repeated Pointer Closing Preserves Gameplay Guard Under Hundred Cats
    ${result}=    Run Tests    PlayMode    CatGame.Tests.PerformanceGuardTests.Two_hundred_CG_pointer_closes_with_one_hundred_cats_block_modal_and_same_frame_inputs
    Should Be Equal As Integers    ${result}[exit_code]    0
    Should Be Equal As Integers    ${result}[passed]    1
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[skipped]    0
