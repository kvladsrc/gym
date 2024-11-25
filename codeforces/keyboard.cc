#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

string shift(string &in, char dir) {
  unordered_map<char, pair<char, char>> table;

  string row1("qwertyuiop");
  string row2("asdfghjkl;");
  string row3("zxcvbnm,./");

  for (size_t i = 0; i < row1.size(); ++i) {
    if (i > 0) {
      table[row1[i]].first = row1[i - 1];
      table[row2[i]].first = row2[i - 1];
      table[row3[i]].first = row3[i - 1];
    } else {
      table[row1[0]].first = '-';
      table[row2[0]].first = '-';
      table[row3[0]].first = '-';
    }

    if (i < row1.size() - 1) {
      table[row1[i]].second = row1[i + 1];
      table[row2[i]].second = row2[i + 1];
      table[row3[i]].second = row3[i + 1];
    } else {
      table[row1[i]].second = '-';
      table[row2[i]].second = '-';
      table[row3[i]].second = '-';
    }
  }

  string res;
  for (char c : in) {
    if (dir == 'R') {
      res += table[c].first;
    } else {
      res += table[c].second;
    }
  }

  return res;
}

int main(int argc, char *argv[]) {
  char dir;
  std::cin >> dir;
  string in;
  std::cin >> in;
  std::cout << shift(in, dir) << "\n";

  return 0;
}
