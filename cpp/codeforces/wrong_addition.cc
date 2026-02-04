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

char itoc(int a) { return a + '0'; }

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int64_t a, s;
    std::cin >> a >> s;

    vector<int> a_digits;
    vector<int> s_digits;

    while (a) {
      a_digits.push_back(a % 10);
      a /= 10;
    }

    bool ok = true;
    for (auto i : a_digits) {
      int s_buf = s % 10;
      s /= 10;
      if (s_buf < i) {
        s_buf += (s % 10) * 10;
        s /= 10;
      }
      if (s_buf >= i && (s_buf - i) < 10) {
        s_digits.push_back(s_buf);
      } else {
        ok = false;
        break;
      }
    }

    if (!ok) {
      std::cout << -1 << "\n";
      continue;
    }

    while (s) {
      s_digits.push_back(s % 10);
      s /= 10;
      a_digits.push_back(0);
    }

    string res;
    for (size_t i = 0; i < a_digits.size(); ++i) {
      res = itoc(s_digits[i] - a_digits[i]) + res;
    }

    // Remove leading zeroes.
    std::cout << std::stoll(res) << "\n";
  }

  return 0;
}
