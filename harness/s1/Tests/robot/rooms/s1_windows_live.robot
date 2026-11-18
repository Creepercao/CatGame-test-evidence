*** Settings ***
Documentation    Ordinary published Windows package: owned foreground mouse, shared debugger commands, autosave, cold load and restore.
Library    Process

*** Test Cases ***
S1-R Ordinary Windows Live Edits Persist And Grow The Room
    ${run}=    Run Process    ${PYTHON}    ${PROBE}    ${OUTPUT DIR}/runtime
    ...    stdout=${OUTPUT DIR}/stdout.txt    stderr=${OUTPUT DIR}/stderr.txt    timeout=23m    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
