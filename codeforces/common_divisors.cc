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
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int64_t gcd(int64_t a, int64_t b) {
  if (a % b == 0) {
    return b;
  }

  return gcd(b, a % b);
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  int64_t common_gcd;
  std::cin >> common_gcd;

  for (int i = 1; i < n; ++i) {
    int64_t buf;
    std::cin >> buf;
    common_gcd = gcd(common_gcd, buf);
  }

  int res = 0;
  for (int64_t i = 1; (i * i) <= common_gcd; ++i) {
    if (common_gcd % i == 0) {
      res++;

      if ((common_gcd / i) != i) {
        res++;
      }
    }
  }

  std::cout << res << "\n";

  return 0;
}
