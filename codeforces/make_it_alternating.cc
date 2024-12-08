#include <algorithm>
#include <climits>
#include <cmath>
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

int64_t factorial(int64_t a, int64_t modulo) {
  int64_t res = 1;
  while (a) {
    res *= a;
    res %= modulo;
    a--;
  }
  return res;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;

  const int64_t modulo = 998244353;

  while (t--) {
    string s;
    std::cin >> s;

    // Idea is to count all continious segments of two or more same
    // characters, then calculate result. First number A is equal to
    // the sum of all segments - number of segments.
    //
    // To calculate the second number we use some combinatorics. Lets
    // assume, that we have to perform A operations, we have A!
    // different permutations on it. But for every segment we should
    // keep one element. Final formula: number of ways to keep one
    // element per segments * A!.
    vector<int64_t> segs;
    char prev;
    if (s.front() == '1') {
      prev = '0';
    } else {
      prev = '1';
    }
    int64_t cur = 1;
    for (auto c : s) {
      if (c == prev) {
        cur++;
      } else if (cur > 1) {
        segs.push_back(cur);
        cur = 1;
      }
      prev = c;
    }

    if (cur > 1) {
      segs.push_back(cur);
    }

    int64_t res1 = 0;
    int64_t res2 = 1;
    for (auto i : segs) {
      res1 += i - 1;
      res2 *= i;
      res2 %= modulo;
    }

    res2 *= factorial(res1, modulo);
    res2 %= modulo;

    std::cout << res1 << " " << res2 << "\n";
  }

  return 0;
}
