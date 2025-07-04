using AutoQuizApi.Data;
using AutoQuizApi.Interfaces;
using AutoQuizApi.Models;
using AutoQuizApi.Repositories;
using AutoQuizApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoQuizApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class QuizController : BaseController
{
    private readonly AutoQuizDbContext _dbcontext;
    private readonly IQuizRepository _quizRepository;
    private readonly IAiQuizService _aiQuiz;
    private readonly IUserRepository _user;

    public QuizController(AutoQuizDbContext dbcontext, IAiQuizService aiQuiz, IQuizRepository quizRepository, IUserRepository user)
    {
        _dbcontext = dbcontext;
        _aiQuiz = aiQuiz;
        _quizRepository = quizRepository;
        _user = user;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadAndGenerateQuiz(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File not found");
        }

        AiQuizDto aiResponse = await _aiQuiz.GenerateQuizFromFileAsync(file);

        string fileExtension = Path.GetExtension(file.FileName);
        string storedFileName = $"{Guid.NewGuid()}{fileExtension}";

        SourceDocument sourceDocument = new()
        {
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            UploadedAt = DateTime.UtcNow
        };

        User user = await _user.GetUserById(GetLoggedInUserId()) ?? throw new Exception("User not logged in");

        Quiz quiz = new()
        {
            Name = aiResponse.QuizTitle,
            SourceDocument = sourceDocument,
            User = user
        };

        foreach (AiQuestionDto question in aiResponse.Questions)
        {
            Question newQuestion = new()
            {
                Text = question.Text,
                Quiz = quiz
            };

            foreach (AiAnswerDto answer in question.Answers)
            {
                Answer newAnswer = new()
                {
                    Text = answer.Text,
                    IsCorrect = answer.IsCorrect,
                    Question = newQuestion
                };
                newQuestion.Answers.Add(newAnswer);
            }
            quiz.Questions.Add(newQuestion);
        }

        await _quizRepository.AddAsync(quiz);
        await _dbcontext.SaveChangesAsync();

        return Ok(aiResponse);
    }
}
