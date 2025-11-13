#include <gtest/gtest.h>

#include "cpp/warmup/lcm.hpp"

TEST(LCMTest, LCMOf4And6Is12) { EXPECT_EQ(warmup::lcm(4, 6), 12); }

TEST(LCMTest, LCMWithOneZero5) { EXPECT_EQ(warmup::lcm(0, 5), 0); }

TEST(LCMTest, LCMWithOneZero0) { EXPECT_EQ(warmup::lcm(5, 0), 0); }

TEST(LCMTest, LCMWithBothZeros) { EXPECT_EQ(warmup::lcm(0, 0), 0); }

TEST(LCMTest, LCMWithNegativeFirstNumber) { EXPECT_EQ(warmup::lcm(-4, 6), 12); }

TEST(LCMTest, LCMWithBothNegatives) { EXPECT_EQ(warmup::lcm(-4, -6), 12); }

TEST(LCMTest, LCMOfCoPrimeNumbers) { EXPECT_EQ(warmup::lcm(7, 3), 21); }

TEST(LCMTest, LCMOfEqualNumbers) { EXPECT_EQ(warmup::lcm(10, 10), 10); }
