*** Settings ***
Documentation    Unity Editor 调试器命令链：功能、模态、备份失败、正常结局、40次操作留证。
Library    ../libraries/UnityTestRunner.py    D:/Unity/2022.3.21f1/Editor/Unity.exe    <PROJECT>    ${OUTPUT DIR}/unity

*** Test Cases ***
Editor Debugger Commands Backup Recovery And Repeated Edits Pass
    ${result}=    Run Tests    PlayMode    CatGame.Tests.EditorDebuggerTests
    Log    ${result}
    Should Be Equal As Integers    ${result}[exit_code]    0
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[passed]    5
    Should Be Equal As Integers    ${result}[skipped]    0
