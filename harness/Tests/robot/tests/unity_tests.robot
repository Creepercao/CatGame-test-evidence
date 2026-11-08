*** Settings ***
Documentation     喵屋（养猫放置游戏）的自动化测试。
...               全部在 Unity 后台进程里跑：不弹窗口、不占鼠标、不抢焦点。
...               PlayMode 验证玩法与界面链路，EditMode 验证场景与美术导入。
Library           ../libraries/UnityTestRunner.py
...               ${UNITY_EXE}    ${PROJECT_PATH}    ${OUTPUT DIR}/unity-main
Suite Setup       Run All Unity Suites

*** Variables ***
${UNITY_EXE}       %{UNITY_EXE=D:\Unity\2022.3.21f1\Editor\Unity.exe}
${PROJECT_PATH}    %{CATGAME_PROJECT=<PROJECT>}
${MIN_PLAYMODE}    ${86}
${MIN_EDITMODE}    ${51}

*** Test Cases ***
PlayMode 套件全部通过
    [Documentation]    玩法与界面交互，一个失败都不允许
    Should Be Equal As Integers    ${PM}[failed]    0
    ...    msg=有 ${PM}[failed] 个用例失败：${PM}[failed_cases]

EditMode 套件全部通过
    [Documentation]    场景配置与美术导入检查
    Should Be Equal As Integers    ${EM}[failed]    0
    ...    msg=有 ${EM}[failed] 个用例失败：${EM}[failed_cases]

测试用例数量没有意外减少
    [Documentation]    防止用例被误删或漏发现
    Should Be True    ${PM}[total] >= ${MIN_PLAYMODE}
    ...    msg=PlayMode 只跑到了 ${PM}[total] 个用例，少于预期的 ${MIN_PLAYMODE} 个
    Should Be True    ${EM}[total] >= ${MIN_EDITMODE}
    ...    msg=EditMode 只跑到了 ${EM}[total] 个用例，少于预期的 ${MIN_EDITMODE} 个

核心玩法与界面链路都被覆盖
    [Documentation]    抽查关键猫实体、购买、HUD 与点击链路
    ${names}=    Get Test Case Names    PlayMode
    FOR    ${required}    IN
    ...    启动后加载三个品种并选中墨墨
    ...    All_twenty_items_purchase_five_levels_and_share_card_marker_art
    ...    Achievement_toast_fades_and_next_queued_message_restores_opacity
    ...    One_shop_scroll_jumps_sections_keeps_free_offset_and_partial_cards
    ...    Items_use_cat_sized_opaque_bounds_ground_anchors_and_feeder_depth
    ...    Main_menu_places_CG_left_and_existing_options_right
    ...    Simultaneous_memories_play_in_order_once_and_pause_all_gameplay
    ...    First_real_pet_unlocks_its_own_CG_and_returns_to_play_without_repeating
    ...    Interrupted_memory_remains_pending_and_reopens_before_later_memories
    ...    Offline_and_intro_ownership_precede_pending_memories
    ...    Historical_seen_milestones_do_not_replay_but_new_one_still_opens
    ...    Rocket_cancel_insufficient_balance_confirm_and_continue_preserve_progress
    ...    Interrupted_ending_recovers_from_both_screens_without_paying_again_and_menu_marks_done
    ...    Rocket_shop_is_same_scroll_pixel_icon_and_fifty_confirmation_cancels_do_not_leak_modals
    ...    Fresh_intro_shows_Momo_teaches_both_incomes_and_blocks_gameplay_until_skip
    ...    Finished_intro_does_not_repeat_and_interrupted_intro_restarts_without_marking_done
    ...    Offline_summary_keeps_modal_ownership_then_pending_intro_can_open
    ...    Modal_gate_restores_original_speed_only_for_its_owner
    ...    Quit_before_intro_completion_updates_clock_but_first_menu_does_not_create_save
    ...    Floor_boundary_move_keeps_pixel_snapped_feet_on_floor
    ...    Room_surface_rejects_walls_props_and_crossing_segments_after_resize
    ...    One_hundred_cats_move_on_floor_without_crossing_furniture
    ...    Pixel_Egypt_loads_all_new_frames_without_changing_other_breeds
    ...    Pixel_Egypt_roams_with_Walk_stops_with_Idle_and_clicks_Jump_once
    ...    Pixel_Egypt_runtime_capture_keeps_size_and_shows_each_action
    ...    Egypt_long_rest_enters_Sleep_and_pause_freezes_it
    ...    Clicking_sleeping_Egypt_wakes_Jumps_once_and_resumes_Walk
    ...    Rest_expires_and_missing_Sleep_or_short_stops_do_not_sleep
    ...    Legacy_forced_Idle_keeps_its_existing_rest_timer
    ...    Milk_preview_and_economy_share_new_four_action_frames
    ...    Milk_world_sleeps_produces_clicks_once_and_resumes_roaming
    ...    All_three_starters_share_preview_economy_and_new_frame_contracts
    ...    Tabby_Snow_purchases_click_once_and_resume_new_Walk
    ...    Tabby_Snow_four_actions_render_with_their_color_roles
    ...    Seven_new_breeds_have_matching_frame_metadata_and_assets
    ...    Patterned_breeds_purchase_jump_click_once_and_walk
    ...    Patterned_breeds_four_actions_render_with_preserved_identity
    ...    All_eleven_breeds_use_new_four_clips_and_old_Calico_Walk_stays_deleted
    ...    Final_four_purchases_jump_click_once_and_use_new_Walk
    ...    Final_four_actions_render_with_patches_cowls_and_Santa_hat
    ...    每个品种的七套动画都齐全
    ...    强制播放每个动作都能出帧
    ...    猫窝显示猫毛产量阶段及全部购买卡片
    ...    购买猫咪扣除价格并增加实体
    ...    购买物品升级扣款并提升等级和产量
    ...    点击猫通过当前点击值增加猫毛
    ...    界面可以接收点击_事件系统与射线检测都在
    ...    Cat_frames_stay_inside_floor_at_all_four_edges
    ...    Roster_keeps_one_actor_per_owned_breed_and_caps_at_100
    ...    Stress_440_owned_cats_survive_100_roster_rebuilds
    ...    Assigning_open_walk_surface_preserves_initial_roaming_distance
    ...    Two_hundred_CG_pointer_closes_with_one_hundred_cats_block_modal_and_same_frame_inputs
    ...    Stress_ten_thousand_click_and_purchase_requests_apply_once
    ...    Newly_purchased_breed_gets_a_visible_actor_when_roster_is_full
    ...    Cat_visuals_scale_32px_art_to_the_same_height_as_64px_art
    ...    Licking_a_cat_adds_click_value_and_shows_a_reward
    ...    超过实体上限时堆叠角标显示猫只数量
    ...    猫达到四十只上限后购买卡禁用
    ...    有足够猫毛时购买按钮能完成升级
    ...    积累足够猫毛后可购买首件产量道具
    ...    成就条件满足时只解锁一次并排入提示
    ...    离线结算弹窗暂停产出并可收下奖励
    ...    切换预览品种不改变已拥有猫_绑定预览使用新品种帧
    ...    齿轮打开暂停菜单并能恢复游戏
    ...    暂停面板图鉴列出全部猫咪
    ...    暂停面板显示全部二十项成就
    ...    设置页可以切换数字格式和音量
    ...    主菜单提供继续新游戏设置与退出入口
    ...    猫窝HUD保留暂停入口并移除公园场景入口
    ...    猫窝平台使用单个Tilemap渲染四十二格
        Should Contain    ${names}    ${required}
    END

数值模型关键用例已纳入 EditMode
    [Documentation]    检查猫价、物品递推、加法产量倍数、阶段点击与离线收益测试均可被发现
    ${names}=    Get Test Case Names    EditMode
    FOR    ${required}    IN
    ...    Cat_price_caps_at_forty
    ...    Item_prices_and_effects_follow_generation_and_level_curves
    ...    Production_items_add_with_effects_matching_each_level
    ...    Production_remains_finite_after_one_hundred_thousand_ticks
    ...    Click_value_ignores_cumulative_thresholds_and_offline_is_capped
    ...    Price_progression_reaches_goal_without_clicks_or_stage_gates
    ...    Catalog_has_eleven_cats_and_twenty_items
    ...    Cat_prices_form_natural_progression_and_keep_growth
    ...    Curve_simulator_rejects_invalid_click_rates
    ...    Game_state_has_versioned_save_fields_and_defaults
    ...    New_game_starts_with_one_Milk_cat
    ...    Achievement_catalog_has_twenty_unique_entries
    ...    Achievement_rules_unlock_once_and_record_id
    ...    Offline_earnings_are_capped_and_clock_never_moves_save_backwards
    ...    Save_path_is_inside_the_CatGame_folder
    ...    Save_round_trips_fields_and_replaces_existing_file
    ...    Stress_save_load_round_trips_one_thousand_cycles_and_recovers_midway_corruption
    ...    Corrupt_save_is_quarantined_and_new_game_is_returned
    ...    Save_with_missing_fields_is_quarantined
    ...    Future_save_version_is_quarantined_instead_of_overwritten
    ...    Save_rejects_out_of_range_cat_counts
    ...    构建列表只包含主菜单和猫窝且主菜单排第一
    ...    Home_pixel_room_has_architecture_six_furniture_pieces_and_bound_surface
    ...    V1_save_migrates_without_replaying_intro_or_historical_CG_and_preserves_progress
    ...    V2_intro_and_pending_story_progress_survive_save_round_trip
    ...    Story_save_rejects_repeated_events_or_nonfinite_playtime
    ...    V2_missing_intro_field_is_quarantined_instead_of_silently_resetting_story
    ...    B4_v2_history_without_CG_markers_is_normalized_but_real_pending_memory_survives
    ...    Every_achievement_has_a_unique_imported_CG_with_matching_title_and_order
    ...    Genuine_all_twenty_unlocks_queue_once_and_remain_seen_after_repeated_evaluations
    ...    Existing_twenty_achievement_save_normalizes_new_historical_CGs_but_preserves_pending_first_pet
    ...    Rocket_checks_balance_not_lifetime_income_and_ten_thousand_requests_charge_once
    ...    Purchased_interrupted_ending_and_pending_CGs_round_trip_without_a_second_charge
    ...    Story_queue_is_stable_and_deduplicates_many_offline_unlock_evaluations
    ...    Ending_save_rejects_missing_snapshot_and_invalid_statistics
        Should Contain    ${names}    ${required}
    END

*** Keywords ***
Run All Unity Suites
    [Documentation]    两个平台各跑一次，结果在用例之间共享
    ${pm}=    Run Tests    PlayMode
    Set Suite Variable    ${PM}    ${pm}
    Log    ${pm}
    ${em}=    Run Tests    EditMode
    Set Suite Variable    ${EM}    ${em}
    Log    ${em}
    IF    ${pm}[failed] > 0
        Log    失败用例: ${PM}[failed_cases]    level=WARN
    END

