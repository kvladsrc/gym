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
  int n;
  std::cin >> n;
  vector<vector<int>> bus(n, vector<int>(5));
  bool done = false;
  for (int i = 0; i < n; ++i) {
    string row;
    std::cin >> row;
    for (int j = 0; j < 5; ++j) {
      if (row[j] != 'O') {
        bus[i][j] = 1;
        continue;
      }

      if (!done && j > 0 && bus[i][j - 1] == 0) {
        bus[i][j - 1] = 2;
        bus[i][j] = 2;
        done = true;
      }
    }
  }

  if (!done) {
    std::cout << "NO"
              << "\n";
    return 0;
  }

  std::cout << "YES"
            << "\n";
  for (auto row : bus) {
    for (int i = 0; i < 5; ++i) {
      switch (row[i]) {
        case 0:
          std::cout << "O";
          break;
        case 1:
          if (i == 2) {
            std::cout << "|";
          } else {
            std::cout << "X";
          }
          break;
        default:
          std::cout << "+";
      }
    }
    std::cout << "\n";
  }

  return 0;
}
