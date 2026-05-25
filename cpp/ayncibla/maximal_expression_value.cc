#include <algorithm>
#include <cassert>
#include <cstddef>
#include <cstdint>
#include <iostream>
#include <string>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::vector;

static int64_t eval(int64_t a, int64_t b, char op) {
  if (op == '*') {
    return a * b;
  }
  if (op == '+') {
    return a + b;
  }
  if (op == '-') {
    return a - b;
  }
  assert(0);
}

static bool is_op(char c) { return (c == '*' || c == '+' || c == '-'); }

struct Result {
  bool max_calculated;
  int64_t max;
  bool min_calculated;
  int64_t min;
};

static int64_t get_maximum_value(const string& /*exp*/, int /*start*/,
                                 int /*end*/,
                                 vector<vector<Result>>& /*table*/);

static int64_t get_minimum_value(const string& exp, int start, int end,
                                 vector<vector<Result>>& table) {
  if (table[start][end].min_calculated) {
    return table[start][end].min;
  }

  if (start == end) {
    table[start][end].min_calculated = true;
    table[start][end].min =
        static_cast<int>(exp[start]) - static_cast<int>('0');
    return table[start][end].min;
  }

  vector<int64_t> cases;

  int i = start;
  int const j = end;
  while (i < j) {
    if (is_op(exp[i])) {
      auto l_max = get_maximum_value(exp, start, i - 1, table);
      auto l_min = get_minimum_value(exp, start, i - 1, table);
      auto r_max = get_maximum_value(exp, i + 1, end, table);
      auto r_min = get_minimum_value(exp, i + 1, end, table);

      cases.push_back(eval(l_max, r_max, exp[i]));
      cases.push_back(eval(l_max, r_min, exp[i]));
      cases.push_back(eval(l_min, r_max, exp[i]));
      cases.push_back(eval(l_min, r_min, exp[i]));
    }

    i++;
  }

  auto min_val = *std::min_element(cases.begin(), cases.end());

  table[start][end].min_calculated = true;
  table[start][end].min = min_val;
  return min_val;
}

int64_t get_maximum_value(const string& exp, int start, int end,
                          vector<vector<Result>>& table) {
  if (table[start][end].max_calculated) {
    return table[start][end].max;
  }

  if (start == end) {
    table[start][end].max_calculated = true;
    table[start][end].max =
        static_cast<int>(exp[start]) - static_cast<int>('0');
    return table[start][end].max;
  }

  vector<int64_t> cases;
  int i = start;
  int const j = end;
  while (i < j) {
    if (is_op(exp[i])) {
      auto l_max = get_maximum_value(exp, start, i - 1, table);
      auto l_min = get_minimum_value(exp, start, i - 1, table);
      auto r_max = get_maximum_value(exp, i + 1, end, table);
      auto r_min = get_minimum_value(exp, i + 1, end, table);

      cases.push_back(eval(l_max, r_max, exp[i]));
      cases.push_back(eval(l_max, r_min, exp[i]));
      cases.push_back(eval(l_min, r_max, exp[i]));
      cases.push_back(eval(l_min, r_min, exp[i]));
    }

    i++;
  }

  auto max_val = *std::max_element(cases.begin(), cases.end());

  table[start][end].max_calculated = true;
  table[start][end].max = max_val;

  return max_val;
}

int main() {
  string s;
  std::cin >> s;
  vector<vector<Result>> table;
  table.resize(s.size(), vector<Result>(s.size()));
  std::cout << get_maximum_value(s, 0, s.size() - 1, table) << '\n';
}
