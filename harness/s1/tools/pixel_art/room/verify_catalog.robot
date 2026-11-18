*** Settings ***
Library    Process

*** Test Cases ***
Catalog Background Matches Twelve Raw Tiles
    ${result}=    Run Process    python    ${CURDIR}/verify_catalog.py    stdout=${OUTPUT DIR}/verification.log    stderr=STDOUT
    Should Be Equal As Integers    ${result.rc}    0
