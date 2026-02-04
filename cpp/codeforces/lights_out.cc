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
  vector<vector<bool>> desk(3, vector<bool>(3, true));
  for (int i = 0; i < 3; ++i) {
    for (int j = 0; j < 3; ++j) {
      int press;
      std::cin >> press;

      while (press--) {
        desk[i][j] = !desk[i][j];
        if (j > 0) desk[i][j - 1] = !desk[i][j - 1];
        if (j < 2) desk[i][j + 1] = !desk[i][j + 1];
        if (i > 0) desk[i - 1][j] = !desk[i - 1][j];
        if (i < 2) desk[i + 1][j] = !desk[i + 1][j];
      }
    }
  }

  for (int i = 0; i < 3; ++i) {
    for (int j = 0; j < 3; ++j) {
      std::cout << (desk[i][j] ? 1 : 0);
    }
    std::cout << "\n";
  }

  return 0;
}
