*** Settings ***
Library    Process

*** Test Cases ***
Isometric Room Props Items And UI Match Their Source Grids
    ${result}=    Run Process    python    ${CURDIR}/verify_room.py    stdout=${OUTPUT DIR}/verification.log    stderr=STDOUT
    Should Be Equal As Integers    ${result.rc}    0    See verification.log
