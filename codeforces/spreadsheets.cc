#include <algorithm>
#include <climits>
#include <cmath>
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
#include <utility>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

bool isDigit(char c) { return c <= '9' && c >= '0'; }

// First type: A1. Second type R1C1.
int get_type(string s) {
  bool digit_found = false;
  for (auto c : s) {
    if (digit_found && !isDigit(c)) return 2;
    if (isDigit(c)) digit_found = true;
  }
  return 1;
}

int decode_a_type_col(string s) {
  int res = 0;
  // A = 1, B = 2, ..., Z = 26.
  for (auto c : s) {
    res *= 26;
    int buf = (c - 'A') + 1;
    res += buf;
  }
  return res;
}

int decode_a_type_row(string s) { return std::stoi(s); }

// res.first is for row, res.second is for col.
pair<int, int> decode_a_type(string s) {
  string col = s;
  string row = "";
  while (isDigit(col.back())) {
    row = col.back() + row;
    col.pop_back();
  }

  pair<int, int> res({decode_a_type_row(row), decode_a_type_col(col)});
  return res;
}

// res.first is for row, res.second is for col.
pair<int, int> decode_b_type(string s) {
  string row = "";
  string col = "";
  while (isDigit(s.back())) {
    col = s.back() + col;
    s.pop_back();
  }
  s.pop_back();  // -> R
  while (isDigit(s.back())) {
    row = s.back() + row;
    s.pop_back();
  }

  pair<int, int> res;
  res.first = std::stoi(row);
  res.second = std::stoi(col);
  return res;
}

string encode_to_a_type(pair<int, int> p) {
  string row = std::to_string(p.first);
  string col;

  while (p.second) {
    int buf = p.second % 26;
    if (buf == 0) {
      // 26 is for Z. p.first cannot be 0.a
      buf = 26;
      p.second--;
    }
    char r = 'A' - 1 + buf;
    p.second /= 26;
    col = r + col;
  }

  return col + row;
}

string encode_to_b_type(pair<int, int> p) {
  string res = "R";
  res += std::to_string(p.first);
  res += 'C';
  res += std::to_string(p.second);
  return res;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string s;
    std::cin >> s;

    if (get_type(s) == 1) {
      auto a = decode_a_type(s);
      std::cout << encode_to_b_type(a) << "\n";
    } else {
      auto a = decode_b_type(s);
      std::cout << encode_to_a_type(a) << "\n";
    }
  }

  return 0;
}
