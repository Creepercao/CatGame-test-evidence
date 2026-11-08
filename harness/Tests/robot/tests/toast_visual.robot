*** Settings ***
Library    ../libraries/UnityTestRunner.py    D:/Unity/2022.3.21f1/Editor/Unity.exe    <PROJECT>    ${OUTPUT DIR}/unity

*** Test Cases ***
Achievement Toast Has Unlock Text Fades And Continues The Queue
    ${result}=    Run Tests    PlayMode    Achievement_toast_fades_and_next_queued_message_restores_opacity
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[passed]    1
    Should Be Equal As Integers    ${result}[exit_code]    0
