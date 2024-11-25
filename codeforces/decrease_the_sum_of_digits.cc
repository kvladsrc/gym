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

int sum_of_digits(int64_t a) {
  int res = 0;
  while (a) {
    res += a % 10;
    a /= 10;
  }
  return res;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int64_t n, s;
    std::cin >> n >> s;

    int64_t rank = 10;
    int64_t delta = 0;
    while (true) {
      if (sum_of_digits(n + delta) <= s) {
        break;
      }
      delta = rank - (n % rank);
      rank *= 10;
    }

    std::cout << delta << "\n";
  }

  return 0;
}
