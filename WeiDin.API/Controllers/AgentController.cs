using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using WeiDin.Core.Interfaces;

namespace WeiDin.API.Controllers;

/// <summary>
/// Agent 问答控制器
///
/// 注意：当前实现为 RAG 单轮问答（检索知识库 + LLM 生成），并非真正的 Agent。
/// 后续可扩展为支持多步推理、工具调用等 Agent 特性。
/// </summary>
[Route("api/v1/[controller]")]
[Authorize]
public class AgentController : AbpControllerBase
{
    private readonly IAgentEngine _agentEngine;
    private readonly ILogger<AgentController> _logger;

    public AgentController(IAgentEngine agentEngine, ILogger<AgentController> logger)
    {
        _agentEngine = agentEngine;
        _logger = logger;
    }

    /// <summary>
    /// 向 Agent 提问（RAG 问答）
    /// 请求体：{ "question": "什么是RAG？", "topK": 5 }
    /// 响应体：{ "answer": "...", "sources": [...], "chunksUsed": 3 }
    /// </summary>
    [HttpPost("ask")]
    public async Task<ActionResult<AgentResponse>> Ask([FromBody] AgentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest("问题不能为空");

            // 委托给 Agent 引擎执行 RAG 管道
            var response = await _agentEngine.AskAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // LLM 配置错误等业务异常
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent 问答失败");
            return StatusCode(500, "问答服务暂时不可用");
        }
    }
}
