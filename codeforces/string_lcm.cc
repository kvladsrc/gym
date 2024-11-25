#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

int gcd(int a, int b) {
  if (a % b == 0) {
    return b;
  }

  return gcd(b, a % b);
}

int lcm(int a, int b) {
  int d = gcd(a, b);
  return (a * b) / d;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string s, t;
    std::cin >> s >> t;

    bool res = true;
    string result;
    for (int i = 0; i < lcm(s.size(), t.size()); ++i) {
      if (s[i % s.size()] != t[i % t.size()]) {
        res = false;
        break;
      }

      result += s[i % s.size()];
    }

    if (res) {
      std::cout << result << "\n";
    } else {
      std::cout << -1 << "\n";
    }
  }

  return 0;
}
