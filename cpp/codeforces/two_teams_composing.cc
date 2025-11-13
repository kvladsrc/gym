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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> skills(n);
    unordered_map<int, int> m;
    int most_used = 0;

    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      skills[i] = buf;

      m[buf]++;
      if (m[buf] > m[most_used]) {
        most_used = buf;
      }
    }

    int first_max = m.size() - 2;  // max element and 0;
    int second_max = m[most_used];
    int res;

    if (second_max - first_max >= 2) {
      res = first_max + 1;
    } else {
      res = min(first_max, second_max);
    }
    if (!res && n >= 2) res = 1;

    std::cout << res << "\n";
  }

  return 0;
}
