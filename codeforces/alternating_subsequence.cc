#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

#define INF 1000000000

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int64_t n;
    std::cin >> n;

    int64_t first;
    std::cin >> first;
    n--;
    bool positive = first > 0 ? true : false;
    int64_t res = 0;
    int64_t max_cur = first;

    while (n--) {
      int64_t buf;
      std::cin >> buf;

      if (positive && buf < 0) {
        res += max_cur;
        positive = false;
        max_cur = buf;
        continue;
      }

      if (!positive && buf > 0) {
        res += max_cur;
        positive = true;
        max_cur = buf;
        continue;
      }

      if (buf > max_cur) max_cur = buf;
    }
    res += max_cur;

    std::cout << res << "\n";
  }
  return 0;
}
