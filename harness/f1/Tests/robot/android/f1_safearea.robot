*** Settings ***
Documentation    Real native 2220x1080 AVD tall cutout; restore overlay and wm size after test.
Library          Process

*** Test Cases ***
F1-SAFE-01 Real Cutout Menu And Home
    ${run}=    Run Process    ${PYTHON}    ${CURDIR}/f1_safearea.py    --adb    ${ADB}    --output    ${OUTPUT DIR}/runtime    stdout=${OUTPUT DIR}/runner.log    stderr=STDOUT    timeout=150s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
