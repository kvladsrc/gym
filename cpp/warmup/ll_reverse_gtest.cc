#include <gtest/gtest.h>

#include <string>

#include "cpp/warmup/ll_reverse.hpp"

using warmup::ll;

TEST(LLReverseTest, Empty) {
  ll l;
  EXPECT_EQ(l.pop(), -1);
}

TEST(LLReverseTest, One) {
  ll l;
  l.append(1);
  testing::internal::CaptureStdout();
  l.print();
  std::string output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "1\n");

  EXPECT_EQ(l.pop(), 1);
  EXPECT_EQ(l.pop(), -1);
}

TEST(LLReverseTest, Simple) {
  ll l;
  for (int i = 1; i <= 10; ++i) {
    l.append(i);
  }

  l.reverse();

  testing::internal::CaptureStdout();
  l.print();
  std::string output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "10 9 8 7 6 5 4 3 2 1\n");

  EXPECT_EQ(l.pop(), 1);

  testing::internal::CaptureStdout();
  l.print();
  output = testing::internal::GetCapturedStdout();
  EXPECT_EQ(output, "10 9 8 7 6 5 4 3 2\n");
}
