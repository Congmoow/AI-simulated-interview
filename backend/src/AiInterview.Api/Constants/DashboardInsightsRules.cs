namespace AiInterview.Api.Constants;

public static class DashboardInsightsRules
{
    public const string ScopeStrategyTargetPreferredWithGlobalFallback = "target_position_preferred_with_global_fallback";
    public const string ActualScopeTargetPosition = "target_position";
    public const string ActualScopeAllReports = "all_reports";
    public const string FallbackReasonTargetPositionHasNoReports = "target_position_has_no_reports";

    public static readonly IReadOnlyList<DashboardStrengthRule> StrengthRules =
    [
        new DashboardStrengthRule(
            "clear_expression",
            "表达清晰",
            "在多次模拟面试中，回答通常较流畅，表达自然，能够把主要观点讲清楚。",
            [
                "表达清晰",
                "沟通自然",
                "语言流畅",
                "叙述完整",
                "表达流畅",
                "表达节奏稳定",
                "面试沟通感较好"
            ]),
        new DashboardStrengthRule(
            "structured_answer",
            "回答结构化",
            "回答常能先给结论，再分点展开，整体条理较清楚。",
            [
                "回答结构清晰",
                "先给结论再展开说明",
                "回答逻辑清晰",
                "条理基本清晰",
                "逻辑清晰"
            ]),
        new DashboardStrengthRule(
            "project_connection",
            "项目关联能力较好",
            "能够把技术点和项目背景联系起来，而不是只停留在孤立知识点。",
            [
                "能够把技术点和项目背景联系起来",
                "结合实际项目经验",
                "结合项目背景",
                "结合项目经验"
            ]),
        new DashboardStrengthRule(
            "position_fit",
            "岗位匹配度较好",
            "岗位相关经历和关键词较匹配，说明你的经历与目标岗位方向比较贴近。",
            [
                "岗位需求存在一定关联",
                "岗位匹配度",
                "简历关键词与岗位需求存在一定关联"
            ])
    ];

    public static readonly IReadOnlyList<DashboardWeaknessRule> WeaknessRules =
    [
        new DashboardWeaknessRule(
            "project_depth",
            "项目深挖不足",
            "项目回答容易停留在表面描述，缺少技术细节、难点处理和方案权衡。",
            "按“背景-目标-方案-难点-结果-反思”模板重构项目回答。",
            [
                "只说做了什么，没有说为什么这样做、遇到什么问题、怎么取舍。",
                "技术细节、难点处理和结果复盘不够具体。"
            ],
            [
                "项目深挖不足",
                "缺少技术细节",
                "项目深度不足",
                "缺少难点分析",
                "项目回答偏表层",
                "项目细节还可以继续深挖"
            ]),
        new DashboardWeaknessRule(
            "technical_depth",
            "底层原理不够深入",
            "面对原理类问题时，解释深度还不够，容易停留在表层结论。",
            "针对薄弱点补一轮底层原理梳理。",
            [
                "知道结论，但对关键机制、边界和推导过程解释不够深入。",
                "原理题回答缺少核心机制与细节展开。"
            ],
            [
                "底层原理解释不够深入",
                "底层机制",
                "原理不够深入",
                "停留在概念层"
            ]),
        new DashboardWeaknessRule(
            "structured_answer",
            "回答不够结构化",
            "面对开放题时，答案容易想到哪说到哪，缺少先总后分的层次。",
            "统一使用“结论 -> 依据 -> 例子 -> 总结”的回答模板。",
            [
                "缺少先总后分、结论优先的表达习惯。",
                "回答层次不够稳定，重点不够突出。"
            ],
            [
                "回答不够结构化",
                "结构不清晰",
                "逻辑跳跃",
                "重点不突出",
                "结论不明确"
            ]),
        new DashboardWeaknessRule(
            "tradeoff_clarity",
            "取舍表达不够明确",
            "方案选择和取舍依据没有讲透，导致回答说服力不足。",
            "回答方案题时，补齐“为什么选这个方案、不选另一个方案”的对比说明。",
            [
                "能说出方案，但取舍依据、约束条件和边界说明不够清晰。",
                "缺少成本、风险、复杂度上的对比表达。"
            ],
            [
                "关键取舍点可以再更明确",
                "方案权衡不够",
                "取舍点不够明确"
            ]),
        new DashboardWeaknessRule(
            "data_quantification",
            "缺少量化成果",
            "结果表达不够具体，缺少性能、效率或业务收益等量化数据支撑。",
            "补充性能、效率、成本、用户收益等量化结果表达。",
            [
                "回答中缺少关键指标，难以体现真实影响。",
                "结果更多是主观描述，缺少可验证数据。"
            ],
            [
                "缺少量化数据意识",
                "没有量化结果",
                "缺少量化成果",
                "结果表达不具体",
                "建立项目数据记忆库"
            ]),
        new DashboardWeaknessRule(
            "response_validity",
            "回答有效性不足",
            "回答出现不可读、无效或严重缺失信息的情况，导致系统无法有效判断真实能力。",
            "排查网络与输入法环境，确保回答内容可读、完整。",
            [
                "回答内容不可读或存在大量无效字符。",
                "连续追问仍无法获得有效信息。"
            ],
            [
                "回答内容不可读",
                "乱码",
                "无效字符",
                "无法获取有效信息"
            ]),
        new DashboardWeaknessRule(
            "project_authenticity",
            "项目真实性支撑不足",
            "项目表述中缺少可核验的职责、场景和数据细节，容易影响可信度。",
            "补齐项目中的职责边界、关键数据和具体落地细节，增强可信度。",
            [
                "项目描述缺少职责范围、技术决策与结果数据的相互印证。",
                "具体场景和个人贡献支撑不足。"
            ],
            [
                "项目真实性存疑"
            ])
    ];

    public static readonly IReadOnlyList<DashboardAbilityDimensionRule> AbilityDimensionRules =
    [
        new DashboardAbilityDimensionRule("expression", "自我表达", ["clarity", "fluency"]),
        new DashboardAbilityDimensionRule("technical_foundation", "技术基础", ["technicalAccuracy"]),
        new DashboardAbilityDimensionRule("project_depth", "项目深度", ["knowledgeDepth", "projectAuthenticity"]),
        new DashboardAbilityDimensionRule("structured_thinking", "逻辑结构", ["logicalThinking"]),
        new DashboardAbilityDimensionRule("adaptability", "临场应变", ["confidence"]),
        new DashboardAbilityDimensionRule("position_fit", "岗位匹配", ["positionMatch"])
    ];
}

public sealed record DashboardStrengthRule(
    string Key,
    string Title,
    string Description,
    string[] Keywords);

public sealed record DashboardWeaknessRule(
    string Key,
    string Title,
    string Description,
    string Suggestion,
    string[] TypicalBehaviors,
    string[] Keywords);

public sealed record DashboardAbilityDimensionRule(
    string Key,
    string Name,
    string[] SourceDimensions);
