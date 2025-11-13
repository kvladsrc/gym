#include <cmath>
#include <limits>

#include "cpp/warmup/sqrt.hpp"
#include "gtest/gtest.h"

TEST(SqrtTest, PerfectSquare4) { EXPECT_NEAR(warmup::sqrt(4), 2.0, 0.0001); }

TEST(SqrtTest, PerfectSquare9) { EXPECT_NEAR(warmup::sqrt(9), 3.0, 0.0001); }

TEST(SqrtTest, PerfectSquare144) {
  EXPECT_NEAR(warmup::sqrt(144), 12.0, 0.0001);
}

TEST(SqrtTest, NonPerfectSquare2) {
  EXPECT_NEAR(warmup::sqrt(2), 1.41421, 0.0001);
}

TEST(SqrtTest, SmallNumber) { EXPECT_NEAR(warmup::sqrt(0.25), 0.5, 0.0001); }

TEST(SqrtTest, Zero) { EXPECT_NEAR(warmup::sqrt(0), 0.0, 0.0001); }

TEST(SqrtTest, NegativeInput) { EXPECT_TRUE(std::isnan(warmup::sqrt(-4))); }

TEST(SqrtTest, LargeNumber) {
  EXPECT_NEAR(warmup::sqrt(1000000), 1000, 0.0001);
}