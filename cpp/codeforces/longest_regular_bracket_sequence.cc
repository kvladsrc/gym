#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  string in;
  std::cin >> in;

  int open = 0;
  int close = 0;

  int best = 0;
  int best_count = 0;

  for (size_t idx = 0; idx < in.size(); ++idx) {
    if (in[idx] == '(') {
      open++;
    } else {
      close++;
    }

    if (close > open) {
      auto valid = open * 2;

      if (valid == best) {
        best_count++;
      } else if (valid > best) {
        best = valid;
        best_count = 1;
      }

      open = 0;
      close = 0;
    }
  }

  if (open == close) {
    auto valid = open * 2;

    if (valid == best) {
      best_count++;
    } else if (valid > best) {
      best = valid;
      best_count = 1;
    }

    open = 0;
    close = 0;
  }

  // Check post loop.
  size_t recheck = open + close;
  open = 0;
  close = 0;

  for (size_t idx = 1; idx <= recheck; ++idx) {
    if (in[in.size() - idx] == '(') {
      open++;
    } else {
      close++;
    }

    if (close < open) {
      auto valid = close * 2;

      if (valid == best) {
        best_count++;
      } else if (valid > best) {
        best = valid;
        best_count = 1;
      }

      open = 0;
      close = 0;
    }
  }

  if (best == 0) {
    best_count = 1;
  }

  std::cout << best << " " << best_count << "\n";

  return 0;
}
