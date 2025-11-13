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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    unordered_map<char, int> m;
    while (n--) {
      char c;
      std::cin >> c;
      m[c]++;
    }

    int solved = std::count_if(m.begin(), m.end(), [](const auto& p) {
      return p.first - 'A' + 1 <= p.second;
    });

    std::cout << solved << "\n";
  }

  return 0;
}
