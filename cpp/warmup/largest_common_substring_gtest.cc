#include <string>

#include "cpp/warmup/largest_common_substring.hpp"
#include "gtest/gtest.h"

using std::string;

namespace warmup {

TEST(LargestCommonSubstring, Basic) {
  EXPECT_EQ(largest_common_substring("AB", "AB"), 2);
  EXPECT_EQ(largest_common_substring("ABC", "AB"), 2);
  EXPECT_EQ(largest_common_substring("AB", "ABC"), 2);
  EXPECT_EQ(largest_common_substring("ABC", "BC"), 2);
  EXPECT_EQ(largest_common_substring("BC", "ABC"), 2);
}

TEST(LargestCommonSubstring, NoOverlap) {
  EXPECT_EQ(largest_common_substring("ABC", "DEF"), 0);
  EXPECT_EQ(largest_common_substring("A", "B"), 0);
}

TEST(LargestCommonSubstring, Empty) {
  EXPECT_EQ(largest_common_substring("", ""), 0);
  EXPECT_EQ(largest_common_substring("A", ""), 0);
  EXPECT_EQ(largest_common_substring("", "B"), 0);
}

TEST(LargestCommonSubstring, Partial) {
  EXPECT_EQ(largest_common_substring("ABCDE", "CDE"), 3);
  EXPECT_EQ(largest_common_substring("ABCDE", "ABC"), 3);
  EXPECT_EQ(largest_common_substring("ABCDE", "BCD"), 3);
}

TEST(LargestCommonSubstring, Repeated) {
  EXPECT_EQ(largest_common_substring("AAAA", "AA"), 2);
  EXPECT_EQ(largest_common_substring("ABAB", "BABA"), 3);
}

}  // namespace warmup
