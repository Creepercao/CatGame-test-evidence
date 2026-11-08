*** Settings ***
Library    ../libraries/UnityTestRunner.py    D:/Unity/2022.3.21f1/Editor/Unity.exe    <PROJECT>    ${OUTPUT DIR}/unity

*** Test Cases ***
Pixel Snapped Feet Remain Inside The Room
    ${result}=    Run Tests    PlayMode    Floor_boundary_move_keeps_pixel_snapped_feet_on_floor
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[passed]    1
    Should Be Equal As Integers    ${result}[exit_code]    0
