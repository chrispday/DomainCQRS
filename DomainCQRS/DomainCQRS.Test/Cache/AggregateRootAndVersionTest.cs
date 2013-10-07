using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoryQ;

namespace DomainCQRS.Test.Cache
{
	[TestClass]
	public class AggregateRootAndVersionTest
	{
		[TestMethod]
		public void AggregateRootAndVersion()
		{
			new Story("Aggregate Root and Version")
				 .InOrderTo("bundle the AR Id, the AR object and Version together")
				 .AsA("Programmer")
				 .IWant("to create a class that has both")

							.WithScenario("Equality")
								 .Given(AnExistingARAndV)
								 .When(ThereIsAnotherARAndVWithTheSameId)
								 .Then(TheSameShouldBeEqual)
									  .And(TheSameHaveTheSameHashcode)

							.WithScenario("Equality with different version")
								 .Given(AnExistingARAndV)
								 .When(ThereIsAnotherARAndVWithTheSameIdButADifferentVersion)
								 .Then(TheDifferentVersionShouldBeEqual)
									  .And(TheDifferentVersionHaveTheSameHashcode)

							.WithScenario("Equality with different AR")
								 .Given(AnExistingARAndV)
								 .When(ThereIsAnotherARAndVWithTheSameIdButADifferentAR)
								 .Then(TheDifferentARShouldBeEqual)
									  .And(TheDifferentARHaveTheSameHashcode)

							.WithScenario("Inequality")
								 .Given(AnExistingARAndV)
								 .When(ThereIsAnotherARAndVWithADifferentId)
								 .Then(TheyShouldNotBeEqual)
									  .And(TheyShouldNotHaveTheSameHashcode)

							.WithScenario("Not equal to null")
								 .Given(AnExistingARAndV)
								 .When(ComparingItToNull)
								 .Then(ItShouldThrowArgumentNullException)
				 .Execute();
		}

		AggregateRootAndVersion existing;
		private void AnExistingARAndV()
		{
			existing = new AggregateRootAndVersion()
			{
				AggregateRoot = new object(),
				AggregateRootId = Guid.NewGuid(),
				LatestVersion = 1
			};
		}

		AggregateRootAndVersion sameId;
		private void ThereIsAnotherARAndVWithTheSameId()
		{
			sameId = new AggregateRootAndVersion()
			{
				AggregateRoot = existing.AggregateRoot,
				AggregateRootId = existing.AggregateRootId,
				LatestVersion = existing.LatestVersion
			};
		}

		private void TheSameShouldBeEqual()
		{
			Assert.IsTrue(existing.Equals(sameId));
		}

		private void TheSameHaveTheSameHashcode()
		{
			Assert.AreEqual(existing.GetHashCode(), sameId.GetHashCode());
		}

		AggregateRootAndVersion diffId;
		private void ThereIsAnotherARAndVWithADifferentId()
		{
			diffId = new AggregateRootAndVersion()
			{
				AggregateRoot = existing.AggregateRoot,
				AggregateRootId = Guid.NewGuid(),
				LatestVersion = existing.LatestVersion
			};
		}

		private void TheyShouldNotBeEqual()
		{
			Assert.IsFalse(existing.Equals(diffId));
		}

		private void TheyShouldNotHaveTheSameHashcode()
		{
			Assert.AreNotEqual(existing.GetHashCode(), diffId.GetHashCode());
		}

		AggregateRootAndVersion nullOne = null;
		private void ComparingItToNull()
		{
		}

		AggregateRootAndVersion sameIdDiffAR;
		private void ThereIsAnotherARAndVWithTheSameIdButADifferentAR()
		{
			sameIdDiffAR = new AggregateRootAndVersion()
			{
				AggregateRoot = new object(),
				AggregateRootId = existing.AggregateRootId,
				LatestVersion = existing.LatestVersion
			};
		}

		AggregateRootAndVersion sameIdDiffVersion;
		private void ThereIsAnotherARAndVWithTheSameIdButADifferentVersion()
		{
			sameIdDiffVersion = new AggregateRootAndVersion()
			{
				AggregateRoot = existing.AggregateRoot,
				AggregateRootId = existing.AggregateRootId,
				LatestVersion = existing.LatestVersion + 1
			};
		}

		private void TheDifferentVersionShouldBeEqual()
		{
			Assert.AreNotEqual(existing.LatestVersion, sameIdDiffVersion.LatestVersion);
			Assert.IsTrue(existing.Equals(sameIdDiffVersion));
		}

		private void TheDifferentVersionHaveTheSameHashcode()
		{
			Assert.AreNotEqual(existing.LatestVersion, sameIdDiffVersion.LatestVersion);
			Assert.AreEqual(existing.GetHashCode(), sameIdDiffVersion.GetHashCode());
		}

		private void TheDifferentARShouldBeEqual()
		{
			Assert.AreNotEqual(existing.AggregateRoot, sameIdDiffVersion.AggregateRoot);
			Assert.IsTrue(existing.Equals(sameIdDiffAR));
		}

		private void TheDifferentARHaveTheSameHashcode()
		{
			Assert.AreNotEqual(existing.AggregateRoot, sameIdDiffVersion.AggregateRoot);
			Assert.AreEqual(existing.GetHashCode(), sameIdDiffAR.GetHashCode());
		}

		private void ItShouldThrowArgumentNullException()
		{
			try
			{
				existing.Equals(nullOne);
				Assert.Fail();
			}
			catch (ArgumentNullException) { }
		}
	}
}
