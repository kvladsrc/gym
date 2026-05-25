#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

struct Result {
  int sum;
  size_t start;
  size_t end;
};

static Result max_sum_interval(vector<int> const& points, size_t start,
                               size_t end) {
  if (start == end) {
    Result res{};
    res.sum = points[start];
    res.start = start;
    res.end = end;
    return res;
  }

  size_t const mid = (start + end) / 2;
  auto first_part = max_sum_interval(points, start, mid);
  auto second_part = max_sum_interval(points, mid + 1, end);

  Result mid_part{};
  int tmp_sum = points[mid];

  mid_part.sum = points[mid];
  mid_part.start = mid;

  for (int i = static_cast<int>(mid) - 1; i >= static_cast<int>(start); --i) {
    tmp_sum += points[i];
    if (mid_part.sum < tmp_sum) {
      mid_part.sum = tmp_sum;
      mid_part.start = i;
    }
  }

  mid_part.sum += points[mid + 1];
  mid_part.end = mid + 1;
  tmp_sum = mid_part.sum;

  for (int i = static_cast<int>(mid) + 2; i <= static_cast<int>(end); ++i) {
    tmp_sum += points[i];
    if (mid_part.sum < tmp_sum) {
      mid_part.sum = tmp_sum;
      mid_part.end = i;
    }
  }

  auto tmp_max = first_part.sum > second_part.sum ? first_part : second_part;
  return tmp_max.sum > mid_part.sum ? tmp_max : mid_part;
}

int main() {
  size_t n = 0;
  std::cin >> n;
  vector<int> points(n);
  for (size_t i = 0; i < n; i++) {
    std::cin >> points[i];
  }
  std::cout << max_sum_interval(points, 0, n - 1).sum << "\n";
}
