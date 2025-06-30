#include <gtest/gtest.h>

#include <algorithm>
#include <vector>

#include "cpp/warmup/dutch_flag.hpp"

TEST(DutchFlagTest, EmptyArray) {
  std::vector<int> data = {};
  warmup::dutch_flag(data);
  EXPECT_TRUE(data.empty());
}

TEST(DutchFlagTest, OneElement1) {
  std::vector<int> data = {1};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({1}));
}

TEST(DutchFlagTest, OneElement2) {
  std::vector<int> data = {2};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({2}));
}

TEST(DutchFlagTest, OneElement3) {
  std::vector<int> data = {3};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({3}));
}

TEST(DutchFlagTest, AlreadySorted) {
  std::vector<int> data = {1, 1, 2, 2, 3, 3};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({1, 1, 2, 2, 3, 3}));
}

TEST(DutchFlagTest, ReverseSorted) {
  std::vector<int> data = {3, 3, 2, 2, 1, 1};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({1, 1, 2, 2, 3, 3}));
}

TEST(DutchFlagTest, MixedOrder) {
  std::vector<int> data = {3, 1, 2, 1, 3, 2, 2, 1, 3, 1};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({1, 1, 1, 1, 2, 2, 2, 3, 3, 3}));
}

TEST(DutchFlagTest, All1s) {
  std::vector<int> data = {1, 1, 1, 1, 1};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({1, 1, 1, 1, 1}));
}

TEST(DutchFlagTest, All2s) {
  std::vector<int> data = {2, 2, 2, 2, 2};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({2, 2, 2, 2, 2}));
}

TEST(DutchFlagTest, All3s) {
  std::vector<int> data = {3, 3, 3, 3, 3};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({3, 3, 3, 3, 3}));
}

TEST(DutchFlagTest, Only1sAnd2s) {
  std::vector<int> data = {2, 1, 1, 2, 1, 2, 1};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({1, 1, 1, 1, 2, 2, 2}));
}

TEST(DutchFlagTest, Only1sAnd3s) {
  std::vector<int> data = {3, 1, 1, 3, 1, 3, 1};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({1, 1, 1, 1, 3, 3, 3}));
}

TEST(DutchFlagTest, Only2sAnd3s) {
  std::vector<int> data = {2, 3, 3, 2, 3, 2, 3};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({2, 2, 2, 3, 3, 3, 3}));
}

TEST(DutchFlagTest, PivotInTheMiddle) {
  std::vector<int> data = {1, 3, 2, 1, 3, 2, 1, 3, 2};
  warmup::dutch_flag(data);
  EXPECT_EQ(data, std::vector<int>({1, 1, 1, 2, 2, 2, 3, 3, 3}));
}
