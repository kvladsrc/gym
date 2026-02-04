#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <exception>
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

    unordered_map<int, vector<int>> table;
    for (int i = 1; i <= n; ++i) {
      int buf;
      std::cin >> buf;
      table[buf].push_back(i);
    }

    int winner = -1, score = 1000000;
    for (auto p : table) {
      if (p.second.size() > 1) continue;
      if (p.first < score) {
        winner = p.second[0];
        score = p.first;
      }
    }

    std::cout << winner << "\n";
  }

  return 0;
}
