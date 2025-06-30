#include <gtest/gtest.h>

#include <cstdint>
#include <vector>

#include "cpp/warmup/kadanes.hpp"

TEST(KadanesTest, EmptyArray) {
  std::vector<int64_t> data = {};
  EXPECT_EQ(warmup::kadanes(data), 0);
}

TEST(KadanesTest, SinglePositiveElement) {
  std::vector<int64_t> data = {42};
  EXPECT_EQ(warmup::kadanes(data), 42);
}

TEST(KadanesTest, SingleNegativeElement) {
  std::vector<int64_t> data = {-5};
  EXPECT_EQ(warmup::kadanes(data), 0);
}

TEST(KadanesTest, AllNegativeArray) {
  std::vector<int64_t> data = {-8, -3, -6, -2, -7};
  EXPECT_EQ(warmup::kadanes(data), 0);
}

TEST(KadanesTest, MixedArray) {
  std::vector<int64_t> data = {2, -1, 2, 3, -9};
  EXPECT_EQ(warmup::kadanes(data), 6);
}

TEST(KadanesTest, AllPositiveArray) {
  std::vector<int64_t> data = {1, 2, 3, 4};
  EXPECT_EQ(warmup::kadanes(data), 10);
}
