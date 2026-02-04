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
  int n, m;
  std::cin >> n >> m;

  vector<vector<char>> desk(n, vector<char>(m));
  for (auto& r : desk) {
    for (auto& c : r) {
      std::cin >> c;
    }
  }

  char row_begin = 'W';
  for (auto& r : desk) {
    char next = row_begin;
    for (auto& c : r) {
      if (c != '-') {
        c = next;
      }
      std::cout << c;
      next = (next == 'W' ? 'B' : 'W');
    }
    std::cout << "\n";
    row_begin = (row_begin == 'W' ? 'B' : 'W');
  }

  return 0;
}
