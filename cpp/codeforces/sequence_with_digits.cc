#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    uint64_t a, k;
    std::cin >> a >> k;

    while (--k) {
      int min_digit = 9, max_digit = 0;
      auto buf = a;
      while (buf) {
        int last_digit = buf % 10;
        if (last_digit < min_digit) min_digit = last_digit;
        if (last_digit > max_digit) max_digit = last_digit;
        buf /= 10;
      }

      if (min_digit == 0) {
        break;
      }

      a += min_digit * max_digit;
    }

    std::cout << a << "\n";
  }

  return 0;
}
