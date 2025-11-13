#include <gtest/gtest.h>

#include "cpp/warmup/gcd.hpp"

TEST(GCDTest, FourAndSixIsTwo) { EXPECT_EQ(warmup::gcd(4, 6), 2); }

TEST(GCDTest, OneZeroFive) { EXPECT_EQ(warmup::gcd(0, 5), 5); }

TEST(GCDTest, FiveZero) { EXPECT_EQ(warmup::gcd(5, 0), 5); }

TEST(GCDTest, BothZeros) { EXPECT_EQ(warmup::gcd(0, 0), 0); }

TEST(GCDTest, NegativeFirstNumber) { EXPECT_EQ(warmup::gcd(-4, 6), 2); }

TEST(GCDTest, BothNegatives) { EXPECT_EQ(warmup::gcd(-4, -6), 2); }

TEST(GCDTest, CoPrimeNumbers) { EXPECT_EQ(warmup::gcd(7, 3), 1); }

TEST(GCDTest, EqualNumbers) { EXPECT_EQ(warmup::gcd(10, 10), 10); }
