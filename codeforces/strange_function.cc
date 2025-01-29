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

int64_t gcd(int64_t a, int64_t b) {
  if (a % b == 0) {
    return abs(b);
  }
  return gcd(b, a % b);
}

int64_t lcm(int64_t a, int64_t b) { return (a * b) / gcd(a, b); }

int main(int /*argc*/, char* /*argv*/[]) {
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int64_t n = 0;
    std::cin >> n;

    /*
      2 is f(i) for all odd i, we have n / 2 + n % 2 odd numbers, etc.

      1:
      2: 1   1   1   1   1   1   1   1   1   1   1   1   1   1   1   1   1   1
      3: 1 2 0 1 2   1 2 0 1 2   1 2 0 1 2   1 2 0 1 2   1 2 0 1 2   1 2 0 1 2
      4: 1 2 3 0 1 2 3 0 1 2 3   1 2 3 0 1 2 3 0 1 2 3   1 2 3 0 1 2 3 0 1 2 3
      5: 1 2 3 4 0 1 2 3 4 0 1 2 3 4 0 1 2 3 4 0 1 2 3 4 0 1 2 3 4 0 1 2 3 4 0
      6: 1 2 3 4 5 0 1 2 3 4 5 0 1 2 3 4 5 0 1 2 3 4 5 0 1 2 3 4 5 0 1 2 3 4 5
      7: 1 2 3 4 5 6 0 1 2 3 4 5 6 0 1 2 3 4 5 6 0 1 2 3 4 5 6 0 1 2 3 4 5 6 0

    */

    int64_t res = 0;
    const int64_t m = 1000000007;
    int64_t l = 1;
    int64_t prev_l = 0;
    int64_t next = 2;

    while (l <= n) {
      prev_l = l;
      l = lcm(l, next);
      auto by_next = l / prev_l - 1;
      auto rem = (n % l) / prev_l;
      res = (res + (n / l) * by_next * next) % m;
      res = (res + rem * next) % m;
      next++;
    }

    prev_l = l;
    l = lcm(l, next);
    auto rem = (n % l) / prev_l;
    res = (res + rem * next) % m;

    std::cout << res << "\n";
  }

  return 0;
}
