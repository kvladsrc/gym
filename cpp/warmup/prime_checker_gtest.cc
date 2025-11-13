#include <gtest/gtest.h>

#include "cpp/warmup/prime_checker.hpp"

TEST(PrimeCheckerTest, Negative) { EXPECT_FALSE(warmup::is_prime(-5)); }

TEST(PrimeCheckerTest, Zero) { EXPECT_FALSE(warmup::is_prime(0)); }

TEST(PrimeCheckerTest, One) { EXPECT_FALSE(warmup::is_prime(1)); }

TEST(PrimeCheckerTest, SmallPrime2) { EXPECT_TRUE(warmup::is_prime(2)); }

TEST(PrimeCheckerTest, SmallComposite4) { EXPECT_FALSE(warmup::is_prime(4)); }

TEST(PrimeCheckerTest, AnotherPrime3) { EXPECT_TRUE(warmup::is_prime(3)); }

TEST(PrimeCheckerTest, LargePrime9973) { EXPECT_TRUE(warmup::is_prime(9973)); }

TEST(PrimeCheckerTest, LargeComposite10000) {
  EXPECT_FALSE(warmup::is_prime(10000));
}

TEST(PrimeCheckerTest, SquareOfPrime25) { EXPECT_FALSE(warmup::is_prime(25)); }

TEST(PrimeCheckerTest, EvenComposite6) { EXPECT_FALSE(warmup::is_prime(6)); }
