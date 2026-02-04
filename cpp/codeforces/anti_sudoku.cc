#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
    vector<vector<int>> sudoku(9, vector<int>(9));
    for (int row = 0; row < 9; ++row) {
      for (int col = 0; col < 9; ++col) {
        char buf;
        std::cin >> buf;
        sudoku[row][col] = buf - '0';
      }
    }

    for (int i = 0; i < 3; ++i) {
      for (int j = 0; j < 3; ++j) {
        int row = i * 3 + j, col = j * 3 + i;
        sudoku[row][col] = (sudoku[row][col] + 1) % 9;
        if (sudoku[row][col] == 0) sudoku[row][col] = 9;
      }
    }

    for (const auto& v : sudoku) {
      for (auto c : v) {
        std::cout << c;
      }
      std::cout << "\n";
    }
  }

  return 0;
}
