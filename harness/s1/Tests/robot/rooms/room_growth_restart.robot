*** Settings ***
Documentation    Recheck fresh playable load using the already observed native-input checkpoint; never repeat desktop input unnecessarily.
Library          Process
Library          RoomGrowthEvidence.py

*** Test Cases ***
S1-WIN-02 Fresh Playable Load From The Recorded Checkpoint
    Prepare Restart Checkpoint    ${ORIGINAL}    ${OUTPUT DIR}/runtime
    ${run}=    Run Process    ${EXE}    -screen-fullscreen    0    -logFile    ${OUTPUT DIR}/restart-player.log
    ...    env:CATGAME_PLAYER_QA_ROOT=${OUTPUT DIR}/runtime
    ...    env:CATGAME_QA_ROOM_THEMES=1    env:CATGAME_S1_SOURCE=${SOURCE}    env:CATGAME_S1_RESTART=1    env:CATGAME_DEBUG_DISABLE=1
    ...    stdout=${OUTPUT DIR}/stdout.txt    stderr=${OUTPUT DIR}/stderr.txt    timeout=60s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
    Verify Restart Probe    ${ORIGINAL}    ${OUTPUT DIR}/runtime    ${SOURCE}
