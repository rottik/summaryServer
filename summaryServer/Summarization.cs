using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using Preprocessing;
using ALGLIB;

namespace Summarization
{
	
	/// <summary>
	/// Abstract summarization method. 
	/// </summary>
	public abstract class SummarizationMethod
	{
        private string lang;
        public abstract string LemmatizedText();
		/// <summary>
		/// Creates the summary from text, given when instance of method was created.
		/// </summary>
		public abstract void CreateSummary();
		/// <summary>
		/// Creates the summary from given text. Using by Luhn method.
		/// </summary>
		/// <param name='text'>
		/// Text to summary.
		/// </param>
		public abstract string[] GetSummaryByCountOfsents(uint countOfsents);
		/// <summary>
		/// Gets the summary by percent of text.
		/// </summary>
		/// <returns>
		/// The summary by percent of text.
		/// </returns>
		/// <param name='percent'>
		/// Percent.
		/// </param>
		public abstract string[] GetSummaryByPercentOfText(uint percent);
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract int CountOfsents();        
	}
		
	/// <summary>
	/// Luhn method.
	/// </summary>
	public class LuhnMethod: SummarizationMethod
	{
        private string lang;
		private string rawText;
        private double medianIDF;
		private string title="významný důležitý výsledek důsledek";
		//private List<string> StopList;
		private Dictionary<string,double> idf;
		private Dictionary<string,double> tf;
        private Dictionary<string, List<string>> synonyms;
		uint pocetDokumentu=0;
		//private List<string> keyWords;
		private string[] sents;
        private Sentence[] sentences;
        public Sentence[] Sentences
        {
            get { return sentences; }
        }

        int id;

        private string[] keywords;
        Preprocessing.Preprocessing prep;
        private int COS;
		string[] separatorsWord=new string[] {" - ", " ","“","\"","„",".",","};
		//private string[] summary;
		//private bool summaryCreated=false;
		//uint[] score;
		Encoding win=Encoding.GetEncoding(1250);		
		
		/// <summary>
		/// Initializes a new instance of the <see cref="Summarization.LuhnMethod"/> class.
		/// </summary>
		public LuhnMethod(Preprocessing.Preprocessing preprocEngine, string text, string title)
		{
            prep = preprocEngine;

            idf = prep.IDF;
            medianIDF = prep.MedianIDF;
            
            rawText = text;
            this.title = title +" " + this.title;
		}

        public override string LemmatizedText()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Sentence s in sentences)
                sb.AppendLine(s.LemmatizedText);
            Console.WriteLine(sb.ToString());
            return sb.ToString();
        }
        		

        public override int CountOfsents()
        {
            return COS;
        }

        public void BoostKeywords()
        {

        }
        
		
		/// <summary>
		/// Creates the summary.
		/// </summary>
		/// <param name='text'>
		/// Text.
		/// </param>
		public override void CreateSummary()
		{
            sents = prep.Raw2sents(rawText);
            //sents = prep.Raw2sents(this.rawText, new StreamReader("abbrevations."+lang, true).ReadToEnd().Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries));
            COS = sents.Length;
            sentences = prep.GetLemma(sents, (int)DateTime.Now.Ticks);
            // sentences = prep.GetLemma(sents,id,"hunspell","cs");
			// vypocet cetnosti slov v sumarizovanym textu
			tf=new Dictionary<string, double>();					
			foreach(Sentence s in sentences)
			{
				foreach(string word in s.Words.Keys)
				{
					try
					{
						tf[word]++;
					}
					catch(KeyNotFoundException)
					{
						tf.Add(word,1);
					}
				}
			}

            string lemTitle = prep.GetLemma(new string[] { this.title }, (int)DateTime.Now.Ticks)[0].LemmatizedText;
            //string lemTitle = prep.GetLemma(new string[] {this.title},(int)DateTime.Now.Ticks,"hunspell","cs")[0].LemmatizedText;
            string[] keyWords = lemTitle.Split(separatorsWord, StringSplitOptions.RemoveEmptyEntries);
			foreach(string kw in keyWords)
			{
				try
				{
					tf[kw.Trim()]=5*tf[kw.Trim()];
				}
				catch(KeyNotFoundException){;}
			}			
		}

		/// <summary>
		/// Gets the summary by count of sents.
		/// </summary>
		/// <returns>
		/// The summary by count of sents.
		/// </returns>
		/// <param name='countOfsents'>
		/// Count of sents.
		/// </param>
		public override string[] GetSummaryByCountOfsents(uint countOfsents)
		{
			if(countOfsents>sents.Length)
				countOfsents=(uint)sents.Length;
			string[] sent=new string[countOfsents];
			int indexMaxima;
            double maximum;
			for(int i=0;i<sent.Length;i++)
			{
				//prepocitat skore
				foreach(Sentence s in sentences)
				{
					double val=0;
					foreach(string word in s.Words.Keys)
					{
                        try
                        {
                            if (idf.ContainsKey(word))
                            {
                                val += tf[word] * idf[word];
                            }
                            else
                            {
                                val += tf[word] * medianIDF;
                            }
                        }
                        catch (NullReferenceException) { 
                            ;
                        }
					}
					s.Score=val;
                }
				//najit maximum
				maximum=sentences[0].Score;
				indexMaxima=0;
                for (int j = 1; j < sentences.Length; j++)
				{
                    if (maximum < sentences[j].Score)
					{
						indexMaxima=j;
                        maximum = sentences[j].Score;
					}
				}
				//pridat maximum do pole vet
                sent[i] = sentences[indexMaxima].Text;
				//hodnoty slov z maxima nastavit na 0
                foreach (string word in sentences[indexMaxima].Words.Keys)
				{
					tf[word]=0;
				}
				
			}		
			return sent;
		}

		/// <summary>
		/// Gets the summary by percent of text.
		/// </summary>
		/// <returns>
		/// The summary by percent of text.
		/// </returns>
		/// <param name='percent'>
		/// Percent.
		/// </param>
		public override string[] GetSummaryByPercentOfText(uint percent)
		{

			return GetSummaryByCountOfsents((uint)(Math.Round((double)(percent*sents.Length)/100)));
		}

        private void ReplaceSynonyms()
        {
            Dictionary<string, string> nahrady=new Dictionary<string,string>();
            foreach( string t in tf.Keys)
            {
                foreach (string substitute in synonyms.Keys)
                {
                    if (synonyms[substitute].Contains(t))
                    {
                        nahrady.Add(t, substitute);
                        ReplaceTerm(t, substitute);
                    }
                }
            }

            foreach (Sentence s in sentences)
            {
                foreach (string word in s.Words.Keys)
                {
                    if (nahrady.ContainsKey(word))
                    {
                        s.ReplaceWord(word, nahrady[word]);
                    }
                }
            }
        }
        
        private void ReplaceTerm(string word, string synonym)
        {
            if (tf.ContainsKey(word))
            {
                double score = tf[word];
                if (tf.ContainsKey(synonym))
                    tf[synonym] = score;
                else
                    tf.Add(synonym, score);
                tf.Remove(word);
            }
        }
         
	}
		
	/// <summary>
	/// LSA
	/// </summary>
	/// <exception cref='Exception'>
	/// Represents errors that occur during application execution.
	/// </exception>
	public class LSA:SummarizationMethod
	{
		static Encoding win=Encoding.GetEncoding(1250);
		string file;
		string jazyk;
        Preprocessing.Preprocessing prep;
        int id;
		string[] titleLem =new string[] {"významný","důležitý","výsledek","důsledek"};	
		string[] summary;

        string[] termy;
        public override string LemmatizedText()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Sentence s in sentences)
                sb.AppendLine(s.LemmatizedText);

            return sb.ToString();
        }
        Sentence[] sentences;
		
		bool useIDF;
		string[] separatorsWord=new string[] {" - ", " ","“","\"","„",".",","};

		/// <summary>
		/// Initializes a new instance of the <see cref="Summarization.LSA"/> class.
		/// </summary>
		/// <param name='text'>
		/// Text.
		/// </param>
		/// <param name='jazyk'>
		/// Jazyk.
		/// </param>
		/// <param name='useCZ_IDF'>
		/// Use C z_ identifier.
		/// </param>
		public LSA(Preprocessing.Preprocessing prepEngine, string text, string title, bool useCZ_IDF)
		{
            prep = prepEngine;
			this.useIDF=useCZ_IDF;
			this.file=text;
			string temp;
			if(title!="")
			{
                if (jazyk == "cz")
                    temp = prep.GetLemma(new string[] { title }, (int)DateTime.Now.Ticks)[0].LemmatizedText;
                //temp = prep.GetLemma(new string[] { title }, (int)DateTime.Now.Ticks, "hunspell", "cs")[0].LemmatizedText;
                else
                    temp = title;
				this.titleLem=temp.Split(separatorsWord,StringSplitOptions.RemoveEmptyEntries);
			}
		}

		/// <summary>
		/// Creates the summary.
		/// </summary>
		public override void CreateSummary ()
		{
			if (file==null)
				throw new Exception("Nebyl zadan text. Pouzijte metodu: CreateSummary (string text)");
			file=file.Replace("\r","");
			file=file.Replace("\n"," ");
           
            string[] lines = prep.Raw2sents(file);
            if(lines==null)
                throw new NullReferenceException("Empty input file.");
            sentences = new Sentence[lines.Length];

                sentences = prep.GetLemma(lines, (int)DateTime.Now.Ticks);
                List<string> tmp = new List<string>();
                foreach (Sentence s in sentences)
                {
                    List<string> words = s.Words.Keys.ToList();
                    tmp = tmp.Union(words).ToList();
                }
                
                termy = tmp.Except(new string[]{"?","(",")","!","[","]","^","*","+"}).ToArray();


            if (sentences == null || sentences.Length == 1)
                throw new NullReferenceException("The Morph. analyzer wasn't able to split input text.\n"+file);

		}
		/// <summary>
		/// Gets the summary by count of sents.
		/// </summary>
		/// <returns>
		/// The summary by count of sents.
		/// </returns>
		/// <param name='countOfsents'>
		/// Count of sents.
		/// </param>
		public override string[] GetSummaryByCountOfsents (uint countOfsents)
		{		
			//int pocetVet=lines.Length/4;
			int pocetVet=(int)countOfsents;
            int pocetRadkuA = termy.Length;
			int pocetSloupeckuA=sentences.Length;
			double[,] A=new double[pocetRadkuA,pocetSloupeckuA];
			double[,] U=new double[pocetRadkuA,pocetVet];
			double[,] VT=new double[pocetVet,pocetSloupeckuA];;
			double[] S=new double[pocetVet];
			int vypoctiU=0;
			int vypoctiVT=1;
			int pridavnaPamet=2;
			
			// vypocti A
			if((jazyk=="cz") && useIDF)
				A=NaplnA(A,true);
			else
				A=NaplnA(A,false);
			alglib.rmatrixsvd(A,pocetRadkuA,pocetSloupeckuA,vypoctiU,vypoctiVT,pridavnaPamet,out S,out U,out VT);
			double[] skore=GenerujSkore(S,VT);
            for (int i = 0; i < skore.Length; i++)
                sentences[i].Score = skore[i];

			List<string> summaryList=new List<string>();
			while(pocetVet>0)
			{
				double max=skore[0];
				int indexOfMax=0;
				for(int i=1;i<skore.Length;i++)
				{
					if(max<skore[i])
					{
						max=skore[i];
						indexOfMax=i;
					}
				}
				summaryList.Add(sentences[indexOfMax].Text);
				skore[indexOfMax]=0;
				pocetVet--;
			}
			summary=new string[summaryList.Count];
			int j=0;
			foreach(string s in summaryList)
				summary[j++]=s;
			return summary;
		}
		/// <summary>
		/// Gets the summary by percent of text.
		/// </summary>
		/// <returns>
		/// The summary by percent of text.
		/// </returns>
		/// <param name='percent'>
		/// Percent.
		/// </param>
		public override string[] GetSummaryByPercentOfText (uint percent)
		{
            if(percent<=100)
                return GetSummaryByCountOfsents((uint)(Math.Round((double)(percent * sentences.Length) / 100)));
            else
                return GetSummaryByCountOfsents((uint)sentences.Length);
		}
		
		/// <summary>
		/// Changes the title.
		/// </summary>
		/// <param name='keyWords'>
		/// Title.
		/// </param>
		public void ChangeKeyWords(string keyWords)
		{
			if(keyWords=="")
				return;
			string temp;
			if(jazyk=="cz")
                temp = prep.GetLemma(new string[] { keyWords },(int) DateTime.Now.Ticks)[0].LemmatizedText;
			else 
				temp=keyWords;
			this.titleLem=temp.Split(separatorsWord,StringSplitOptions.RemoveEmptyEntries);
		}
        		
		private double[] GenerujSkore(double[] S, double[,] VT)
		{
			double[] skore=new double[VT.GetLength(1)];
			for(int r=0;r<VT.GetLength(1);r++)
			{
				double suma=0;
				for(int i=0;i<VT.GetLength(0);i++)
				{
					suma+=VT[i,r]*VT[i,r]*S[i]*S[i];
				}
				skore[r]=Math.Sqrt(suma);
			}
			return skore;
		}

        public override int CountOfsents()
        {
            return sentences.Length;
        }
		
		private double[,] NaplnA(double[,] A, bool useLuhn)
		{
			if(useLuhn)
				return NaplnA_UseLuhn(A);
			else
				return NaplnA_DontUseLuhn(A);		
		}


		private double[,] NaplnA_DontUseLuhn(double[,] A)
		{
			
			string[] linesWS=new string[sentences.Length];
			int l=0;
			foreach(Sentence line in sentences)
				linesWS[l++]=line.LemmatizedText.ToLower();		
			
			//odstraneni stop slov
            foreach (string stopW in prep.Stoplist) 
			{
				string stop="[^\\w]"+stopW+"[^\\w]";
				if(Regex.IsMatch(file,stop))
				{
					for(int i=0;i<linesWS.Length;i++)
					{
						while(Regex.IsMatch(linesWS[i],stop))
							linesWS[i]=Regex.Replace(linesWS[i],stop," ");
					}
				}
			}			
			
			//spocitej termovou frekvenci
			Dictionary<string,double> tf=new Dictionary<string, double>();
			foreach(string veta in linesWS)
			{
				foreach(string term in termy)
				{
					if(veta.Contains(term))
					{
						try
						{
							tf[term]++;
						}
						catch(KeyNotFoundException)
						{
							tf.Add(term,1);
						}
					}
				}				
			}
			
			//zvyhodni klicova slova
			foreach(string kw in titleLem)
			{
				if(tf.ContainsKey(kw.Trim()))
					tf[kw.Trim()]*=5;
			}
			
			//generuj hodnoty matice A
			for(int i=0;i<A.GetLength(1);i++)
			{
				for(int j=0;j<A.GetLength(0);j++)
				{
					if(linesWS[i].Contains(termy[j]))
					{
						int pocetVyskytu=Regex.Matches(linesWS[i],termy[j]).Count;	
						try
						{
							A[j,i]=pocetVyskytu/tf[termy[j]];
						}
						catch (KeyNotFoundException)
						{
							if(termy[j].Length>2)
								A[j,i]=pocetVyskytu*tf[termy[j]];
						}
					}
				}
			}
			return A;
			
		}
		
		private double[,] NaplnA_UseLuhn(double[,] A)
		{
			//nacti slovnik
			Dictionary<string,double> idf=new Dictionary<string, double>();
			TextReader tr=new StreamReader("ldf",win);
			string line=tr.ReadLine();
			int pocetDokumentu=Convert.ToInt32(line.Substring(line.IndexOf(":")+1));
			while((line=tr.ReadLine())!=null)	
			{
				string[] patrs=line.Split('\t');
				double val=Math.Log((double)pocetDokumentu/Convert.ToDouble(patrs[1]));
				try
				{
					idf.Add(patrs[0],val);
				}
				catch(Exception e)
				{
					Console.WriteLine(patrs[0]+": "+e.Message);
				}
			}
			
			Dictionary<string,double> tf=new Dictionary<string, double>();
			foreach(Sentence veta in sentences)
			{
				foreach(string term in termy)
				{
					if(veta.LemmatizedText.Contains(term))
					{
						try
						{
							tf[term]++;
						}
						catch(KeyNotFoundException)
						{
							tf.Add(term,1);
						}
					}
				}				
			}
			
			//zvyhodni klicova slova
			foreach(string kw in titleLem)
			{
				if(tf.ContainsKey(kw.Trim()))
					tf[kw.Trim()]*=5;
			}
			
			//generuj hodnoty matice A
			for(int i=0;i<A.GetLength(1);i++)
			{
				for(int j=0;j<A.GetLength(0);j++)
				{
					if(sentences[i].LemmatizedText.Contains(termy[j]))
					{
                        int pocetVyskytu = Regex.Matches(sentences[i].LemmatizedText.Replace("?"," "), termy[j]).Count;	
						
						try
						{
							A[j,i]=pocetVyskytu*tf[termy[j]]*idf[termy[j]];
						}
						catch (KeyNotFoundException)
						{
							if(termy[j].Length>2)
								A[j,i]=pocetVyskytu*tf[termy[j]]*(Math.Log(pocetDokumentu));
						}
					}
				}
			}
			return A;
		}
	}
	
	/// <summary>
	/// Třída Heuristic implementuje heuristickou metodu vytváření sumarizačního extraktu z textu.
	/// </summary>
	public class Heuristic :SummarizationMethod
	{
		private string rawText;
		private string title="významný důležitý výsledek důsledek";
		private string lang;
        private string[] sents;
        int id;
        private Sentence[] sentences;

        public Sentence[] Sentences
        {
            get { return sentences; }
        }
        Preprocessing.Preprocessing prep;
		private string[] summary;
		uint[] score;
		//Encoding win=Encoding.GetEncoding(1250);
		string[] separatorsWord=new string[] {" - ", " ","“","\"","„",".",","};
		//string[] separatorssents=new string[] {". ","! ","? ",".\n","!\n","?\n","\n"};
		//private List<KeyValuePair<String, uint>> myList;
		Dictionary<string,uint> wordsCount;
		
		/// <summary>
		/// Initializes a new instance of the <see cref="Summarization.Heuristic"/> class.
		/// </summary>
		/// <param name='rawText'>
		/// Raw text.
		/// </param>
		/// <param name='title'>
		/// Title.
		/// </param>
		/// <param name='lang'>
		/// Language of input text.
		/// </param>
		public Heuristic(Preprocessing.Preprocessing prepEngine, string rawText, string title)
		{
			this.rawText=rawText;
			if(title!="")
				this.title=title;
			else
				this.title=null;
            prep = prepEngine;
		}


        public override string LemmatizedText()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Sentence s in sentences)
                sb.AppendLine(s.LemmatizedText);

            return sb.ToString();
        }
		        	
		/// <summary>
		/// Creates the summary.
		/// </summary>
		public override void CreateSummary()
		{
            sents = prep.Raw2sents(this.rawText);
			sentences=new Sentence[sents.Length];
            string[] tmp=new string[sents.Length];
                sentences = prep.GetLemma(sents, (int)DateTime.Now.Ticks);

			if(title==null)
			{
                title = "";
			}
                this.title = prep.GetLemma(new string[] { this.title }, (int)DateTime.Now.Ticks)[0].LemmatizedText;
			this.RemoveStopList(lang);
			this.CreateWordList(sents);	
			this.AddTitleWords();
			this.CalculatesentsScore();
			//this.WriteStopList2File("slovnik.wrd");
		}
		
		
		/// <summary>
		/// Gets the summary by percent of text.
		/// </summary>
		/// <returns>
		/// The summary by percent of text.
		/// </returns>
		/// <param name='percent'>
		/// Percent.
		/// </param>
		public override string[] GetSummaryByPercentOfText(uint percent)
		{
			if (summary==null)
				CreateSummary();
			return GetSummaryByCountOfsents((uint)(Math.Round((double)(percent*sents.Length)/100)));
		}
		
		/// <summary>
		/// Gets the summary.
		/// </summary>
		/// <returns>
		/// The summary.
		/// </returns>
		/// <param name='countOfsents'>
		/// Count of sents in the summary.
		/// </param>
		public override string[] GetSummaryByCountOfsents(uint countOfsents)
		{
			summary=GetBestsents(countOfsents);
			return summary;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int CountOfsents()
        {
            return sents.Length;
        }
		
		private void CreateWordList(string[] sents)
		{
			wordsCount=new Dictionary<string, uint>();
			
			// ziskej cetnosti vyskytu slov a zapis do slovniku
			for (int i=0;i<sents.Length;i++)
			{
				string[] words=sents[i].Split(separatorsWord,StringSplitOptions.RemoveEmptyEntries);
				for(int w=0;w<words.Length;w++)
				{
					string word=words[w].ToLower();
					word=word.Trim();
					try
					{
						wordsCount[word]++;
					}
					catch (KeyNotFoundException)
					{
						wordsCount.Add(word,1);
					}
				}
			}
		}
		
		private void RemoveStopList(string lang)
		{
            string[] stopWords = prep.Stoplist.ToArray();
            for (int s = 0; s < sentences.Length; s++)
			{
                sentences[s].LemmatizedText = sentences[s].LemmatizedText.ToLower();
				foreach (string stop in stopWords)	
				{
					try
					{
                        sentences[s].LemmatizedText = Regex.Replace(sentences[s].LemmatizedText, "[^\\w]" + stop + "[^\\w]", " ");
						//esents[s]=esents[s].Replace(" "+stop+" ","");
					}
					catch (Exception)
					{	;	}
				}
			}					
		}
		
		private void AddStopList(string lang)
		{
            string[] stopWords = prep.Stoplist.ToArray();	
			
			// pridej stop slova
			foreach (string stop in stopWords)
			{
				string stopEdit=stop.Trim();
				stopEdit=stopEdit.ToLower();
				try
				{	wordsCount[stopEdit]=0;	}
				catch (KeyNotFoundException)
				{ 
					; //stop slova neni nutno pridavat do slovniku vsechny, nektera slova v textu nemusi byt
				}
			}
		}
		
		private void AddTitleWords()
		{
			uint maxScore=0;
			foreach( KeyValuePair<string, uint> kvp in wordsCount )
				if(kvp.Value>maxScore)
					maxScore=kvp.Value;
			
			//pridani slov z nadpisu do slovniku
			string[] titleWords=title.Split(separatorsWord,StringSplitOptions.RemoveEmptyEntries);
			foreach (string titleword in titleWords)
			{
				try
				{	wordsCount[titleword.ToLower()]=maxScore*2;	}
				catch (KeyNotFoundException)
				{ ; }
			}
		}
		
		/// <summary>
		/// Writes the WordList in to file order by frequency of words.
		/// </summary>
		/// <param name='path'>
		/// Path to save WordList file.
		/// </param>
		public void WriteWordList2File(string path)
		{
			//vytvoreni a setrideni slovniku pro ucel zapisu do souboru
			List<KeyValuePair<String, uint>> myList = new List<KeyValuePair<String,uint>>();
			foreach( KeyValuePair<string, uint> kvp in wordsCount )
				myList.Add(kvp);			
			myList.Sort(delegate(KeyValuePair<String, uint> x, KeyValuePair<String, uint> y) { return y.Value.CompareTo(x.Value); });
			
			//zapis slovniku do souboru
			TextWriter ww=new StreamWriter(path);
        	foreach( KeyValuePair<string, uint> kvp in myList )
        	{
            	ww.WriteLine("{0} : {1}",kvp.Key, kvp.Value);
	        }
			ww.Close();
		}
		
		private void CalculatesentsScore()
		{
			// ohodnot vety
			score=new uint[sents.Length];
            /*
            sentences = new Sentence[sents.Length];
            for (int i = 0; i < sents.Length; i++)
                sentences[i] = new Sentence(sents[i], sents[i]);
            */
			for (int i=0;i<sents.Length;i++)
			{
                string[] words = sentences[i].Words.Keys.ToArray();
				foreach (string word in words)
				{
					string wordEdit=word.Trim();
					wordEdit=wordEdit.ToLower();
					try{
					score[i]+=wordsCount[wordEdit];
					}
					catch (KeyNotFoundException)
					{
						//Console.WriteLine("Program tries to get the value of words not in dictionary! word="+wordEdit);
					}
				}
			}
		}
		
		/// <summary>
		/// Gets the best sents.
		/// </summary>
		/// <returns>
		/// The best sents.
		/// </returns>
		/// <param name='count'>
		/// Count of sents in summary.
		/// </param>
		public string[] GetBestsents(uint count)
		{
			if (score==null)
				CalculatesentsScore();
			//hledej vety s nejvetsim skore
			string[] sum=new string[count];
			uint[] tempScore=new uint[score.Length];
			score.CopyTo(tempScore,0);
			uint selected=0;
			while (selected<count)
			{
				uint max=0;
				for (uint i=0;i<tempScore.Length;i++)
				{
					if ((tempScore[max]<tempScore[i])&&(sents[i].Trim()!=""))
						max=i;
				}
				sum[selected++]=sents[max];
				tempScore[max]=0;
			}
			return sum;
		}
	}
	
	/// <summary>
	/// Random sents.
	/// </summary>
	/// <exception cref='NotImplementedException'>
	/// Is thrown when a requested operation is not implemented for a given type.
	/// </exception>
	public class Randomsents:SummarizationMethod
	{

        public override string LemmatizedText()
        {
            StringBuilder sb = new StringBuilder();
            return sb.ToString();
        }
		private string rawText;
		private bool prepared=false;
		private string [] sents;
        Preprocessing.Preprocessing prep;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int CountOfsents()
        {
            return sents.Length;
        }

		/// <summary>
		/// Initializes a new instance of the <see cref="Summarization.Randomsents"/> class.
		/// </summary>
		/// <param name='text'>
		/// Text.
		/// </param>
		public Randomsents(Preprocessing.Preprocessing prepEngine,string text)
		{
            prep = prepEngine;
			rawText=text;
		}
		/// <summary>
		/// Creates the summary.
		/// </summary>
		public override void CreateSummary ()
		{
            sents = prep.Raw2sents(rawText);	
			prepared=true;
		}

		/// <summary>
		/// Gets the summary by count of sents.
		/// </summary>
		/// <returns>
		/// The summary by count of sents.
		/// </returns>
		/// <param name='countOfsents'>
		/// Count of sents.
		/// </param>
		public override string[] GetSummaryByCountOfsents (uint countOfsents)
		{
			if (!prepared)
			{
				Console.WriteLine("Text is not loaded.");
				return new string[] {};
			}
			string[] summary=new string[(int)countOfsents];
			Random rnd=new Random();
			List<int> poradi=new List<int>();
			int volny=0;
			while(poradi.Count<countOfsents)
			{
				int index=rnd.Next(0,sents.Length);
				if(!poradi.Contains(index))
				{
					poradi.Add(index);
					summary[volny++]=sents[index];
				}
			}
			return summary;
		}
		/// <summary>
		/// Gets the summary by percent of text.
		/// </summary>
		/// <returns>
		/// The summary by percent of text.
		/// </returns>
		/// <param name='percent'>
		/// Percent.
		/// </param>
		public override string[] GetSummaryByPercentOfText (uint percent)
		{
			return GetSummaryByCountOfsents((uint)(Math.Round((double)(percent*sents.Length)/100)));
		}
	}	
		
    
}
