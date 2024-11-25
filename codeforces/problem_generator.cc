#include <algorithm>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, m;
    std::cin >> n >> m;

    unordered_map<char, int> table;
    table['A'] = 0;
    table['B'] = 0;
    table['C'] = 0;
    table['D'] = 0;
    table['E'] = 0;
    table['F'] = 0;
    table['G'] = 0;

    while (n--) {
      char buf;
      std::cin >> buf;
      table[buf]++;
    }

    int res = 0;
    for (auto p : table) {
      if (p.second < m) {
        res += m - p.second;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
