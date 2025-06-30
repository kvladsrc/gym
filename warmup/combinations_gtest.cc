#include <gtest/gtest.h>

#include "cpp/warmup/combinations.hpp"

TEST(CombinationsTest, C0_0) { EXPECT_EQ(warmup::combinations(0, 0), 1); }

TEST(CombinationsTest, C5_2) { EXPECT_EQ(warmup::combinations(5, 2), 10); }

TEST(CombinationsTest, C10_3) { EXPECT_EQ(warmup::combinations(10, 3), 120); }

TEST(CombinationsTest, C10_10) { EXPECT_EQ(warmup::combinations(10, 10), 1); }

TEST(CombinationsTest, C10_0) { EXPECT_EQ(warmup::combinations(10, 0), 1); }

TEST(CombinationsTest, C5_6) { EXPECT_EQ(warmup::combinations(5, 6), 0); }

TEST(CombinationsTest, C100_50) {
  EXPECT_EQ(warmup::combinations(100, 50), 538992043);
}
