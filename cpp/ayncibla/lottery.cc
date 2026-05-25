#include <boost/range/algorithm/sort.hpp>
#include <cstdlib>
#include <iostream>
#include <vector>

using std::vector;

static int binary_search_skip_start(const vector<int>& a, int point) {
  int right = a.size() - 1;
  int left = 0;
  int mid = right / 2;

  if (a[right] <= point) {
    return 0;
  }

  while ((right - left) > 1) {
    if (a[mid] > point) {
      right = mid;
    }
    if (a[mid] <= point) {
      left = mid;
    }
    mid = (right + left) / 2;
  }

  if (a[left] <= point) {
    // left := pos, adding 0 element.
    return a.size() - (left + 1);
  }

  // Edge case: point starts before all segments.
  return a.size();
}

static int binary_search_skip_end(const vector<int>& a, int point) {
  int right = a.size() - 1;
  int left = 0;
  int mid = right / 2;

  if (a[right] < point) {
    return a.size();
  }

  while ((right - left) > 1) {
    if (a[mid] >= point) {
      right = mid;
    }
    if (a[mid] < point) {
      left = mid;
    }
    mid = (right + left) / 2;
  }

  if (left > 0) {
    return left + 1;
  }

  if (a[left] < point) {
    return 1;
  }

  return 0;
}

static vector<int> fast_count_segments(vector<int>& starts, vector<int>& ends,
                                       const vector<int>& points) {
  vector<int> cnt(points.size());
  boost::range::sort(starts);
  boost::range::sort(ends);

  for (size_t i = 0; i < points.size(); ++i) {
    int const skiped_by_start = binary_search_skip_start(starts, points[i]);
    int const skiped_by_end = binary_search_skip_end(ends, points[i]);
    cnt[i] = starts.size() - skiped_by_end - skiped_by_start;
  }

  return cnt;
}

static vector<int> naive_count_segments(const vector<int>& starts,
                                        const vector<int>& ends,
                                        const vector<int>& points) {
  vector<int> cnt(points.size());
  for (size_t i = 0; i < points.size(); i++) {
    for (size_t j = 0; j < starts.size(); j++) {
      cnt[i] +=
          static_cast<int>(starts[j] <= points[i] && points[i] <= ends[j]);
    }
  }
  return cnt;
}

int main() {
  int n = 0;
  int m = 0;
  std::cin >> n >> m;
  vector<int> starts(n);
  vector<int> ends(n);
  for (size_t i = 0; i < starts.size(); i++) {
    std::cin >> starts[i] >> ends[i];
  }
  vector<int> points(m);
  for (int& point : points) {
    std::cin >> point;
  }
  // use fast_count_segments
  vector<int> const cnt = fast_count_segments(starts, ends, points);
  for (int const i : cnt) {
    std::cout << i << ' ';
  }
}
