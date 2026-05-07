using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using QuizApi.Models;
using QuizApi.Data;
using Microsoft.EntityFrameworkCore;
using QuizApi.Models.EnumModel;
using Microsoft.AspNetCore.Authorization;

namespace QuizApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class QuizController : ControllerBase
    {
        private QuizAppDbContext _dbContext;
        public QuizController(QuizAppDbContext dbContext)
        {
            this._dbContext = dbContext;
        }

        [HttpGet]
        [Authorize(Policy = "UserFreePolicy")]
        public IActionResult GetQuizQuestions(){
            var Questions = _dbContext.Quiz
                    .Include(x=>x.Options)
                    .Include(y=>y.QnCorrectOption)
                    .Include(z=>z.QnCategory)
                    .Include(a=>a.AnswerKeyExplanation)
                    .ToList();
            return Ok(Questions);
        }
        [HttpPost]
        [Route("getsectionqn/{sectionid}")]
        [Authorize(Policy = "UserFreePolicy")]
        public IActionResult GenarateQuiz(Part sectionid){
            var SecQns = _dbContext.Quiz
                             .Where(qu=>qu.QnCategory.Section==sectionid)
                             .Include(x =>x.QnCorrectOption)
                             .Include(y=>y.Options) 
                             .Include(z=>z.QnCategory)
                             .Include(a=>a.AnswerKeyExplanation)
                             .ToList();


            return Ok(SecQns);
        }

        [HttpPost]
        [Authorize(Policy = "AdminEditPolicy")]
        public IActionResult AddQuestion(QuizQuestionDTO QuizQuestion){
            List<Option> optionsList = new List<Option>();
            
            CorrectOption cp = new CorrectOption();
            for(int i=0 ;i<4;i++)
            {  
                Option opt = new Option();
                opt.OptionValue = QuizQuestion.OptionValues[i];
                opt.OptionId=Guid.NewGuid();
                if( QuizQuestion.CorrectOptionValue!= null && String.Equals(opt.OptionValue,QuizQuestion.CorrectOptionValue)){
                    cp.CorrOptionId =opt.OptionId;
                }
                optionsList.Add(opt);
            }
            cp.CorrectOptionId=Guid.NewGuid();
            cp.CorrectOptionValue = QuizQuestion.CorrectOptionValue;
            Quiz newQuestion = new Quiz(){
                Id= Guid.NewGuid(),
                Question= QuizQuestion.Question,
                Options= optionsList,
                QnCorrectOption= cp,
                CorrectOptionType= QuizQuestion.CorrectOptionType, 
                QnCategory = new Category(){
                        Section=QuizQuestion.Section,
                        GKUnitName=QuizQuestion.GKUnitNum,
                        TamilUnitName=QuizQuestion.TamilUnitNum,
                        MathsUnitName=QuizQuestion.MathsUnitNum
                        }
                };
            if (!string.IsNullOrEmpty(QuizQuestion.AnswerExplanationHtml))
            {
                newQuestion.AnswerKeyExplanation = new AnswerKeyExplanation
                {
                    QuestionId = newQuestion.Id,
                    AnswerExplanationHtml = QuizQuestion.AnswerExplanationHtml
                };
            }
            _dbContext.Quiz.Add(newQuestion);
            _dbContext.SaveChanges();
            return Ok(newQuestion);
        }

        [HttpDelete]
        public IActionResult DeleteQuestion(Guid id){
            var  ques = _dbContext.Quiz.Find(id);                     
            if(ques is null)
                return NotFound();
            _dbContext.Quiz.Remove(ques);
            _dbContext.SaveChanges();
            return Ok(ques);
        }
        [HttpPut]
        public IActionResult UpdateQuestion(Quiz UpdatedQueston)
        {
            _dbContext.Quiz.Update(UpdatedQueston);
            _dbContext.SaveChanges();
            return Ok(UpdatedQueston);
        }
    }
}