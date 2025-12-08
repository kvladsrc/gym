#include <gtest/gtest.h>

#include <string>

#include "cpp/warmup/sliding_window.hpp"

using std::string;
using warmup::find_max;

TEST(SlidingWindowTest, Basic) {
  // string: aabbaa, k=1
  // change 'b' to 'a'
  // aabbaa -> aaabaa (len 3) or aabbaa -> aabbaa (len 2)
  // Wait, logic is: find max length of consecutive chars NOT equal to c,
  // allowing k changes? Or find max length of consecutive chars equal to
  // something by changing at most k chars?

  // Code:
  // if s[r] == c -> bk++ (count of 'c' in window)
  // if bk <= k -> valid window (at most k 'c's)
  // So it finds max length of substring with at most k occurrences of 'c'.
  // Effectively, if we want a substring of 'x', we treat 'c' as the other char
  // we need to change. The main function called: max(find_max(s, 'a', k),
  // find_max(s, 'b', k)) find_max(s, 'a', k) -> counts 'a's. If 'a' count <= k,
  // it means we have at most k 'a's, so we can change them to 'b's. So
  // find_max(s, 'a', k) finds max length of 'b's with k changes.

  string s = "aabbaa";
  int k = 1;
  // Max 'a's (change 'b's): find_max(s, 'b', k)
  // Max 'b's (change 'a's): find_max(s, 'a', k)

  EXPECT_EQ(find_max(s, 'b', k), 3);  // aab -> aaa (change 1 b)
  EXPECT_EQ(find_max(s, 'a', k), 3);  // bba -> bbb (change 1 a)
}

TEST(SlidingWindowTest, AllSame) {
  string s = "aaaaa";
  int k = 2;
  EXPECT_EQ(find_max(s, 'b', k), 5);  // 0 'b's, valid
}

TEST(SlidingWindowTest, AllDifferent) {
  string s = "bbbbb";
  int k = 2;
  EXPECT_EQ(find_max(s, 'b', k),
            2);  // can only have 2 'b's? No, find_max(s, 'b', k) counts 'b's.
  // If we want max length of 'a's, we call find_max(s, 'b', k) -> counts 'b's.
  // If input is all 'b's, and we want 'a's, we need to change 'b's.
  // We can change k=2 'b's. So max length 2.
  EXPECT_EQ(find_max(s, 'b', k), 2);
}
